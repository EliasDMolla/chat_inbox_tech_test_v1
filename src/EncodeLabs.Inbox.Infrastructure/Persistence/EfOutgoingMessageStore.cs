using EncodeLabs.Inbox.Application.OutgoingMessages;
using EncodeLabs.Inbox.Application.Realtime;
using EncodeLabs.Inbox.Domain.Contacts;
using EncodeLabs.Inbox.Domain.Conversations;
using EncodeLabs.Inbox.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

internal sealed class EfOutgoingMessageStore(AppDbContext dbContext) : IOutgoingMessageStore
{
    public async Task<IOutgoingMessageSession?> LockConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
                candidate => candidate.Id == conversationId,
                cancellationToken);

            if (conversation is null)
            {
                await transaction.CommitAsync(cancellationToken);
                await transaction.DisposeAsync();
                return null;
            }

            await AcquireChatLockAsync(conversation.TelegramChatId, cancellationToken);
            await dbContext.Entry(conversation).ReloadAsync(cancellationToken);

            var contact = await dbContext.Contacts.AsNoTracking().SingleAsync(
                candidate => candidate.Id == conversation.ContactId,
                cancellationToken);

            return new Session(dbContext, transaction, conversation, contact);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private Task AcquireChatLockAsync(long telegramChatId, CancellationToken cancellationToken)
    {
        var lockKey = $"chat:{telegramChatId}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    private sealed class Session(
        AppDbContext dbContext,
        IDbContextTransaction transaction,
        Conversation conversation,
        Contact contact) : IOutgoingMessageSession
    {
        private bool completed;

        public long TelegramChatId => conversation.TelegramChatId;

        public async Task<OutgoingMessagePersistenceResult> PersistAsync(
            long telegramMessageId,
            string text,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            if (completed)
            {
                throw new InvalidOperationException("The outgoing message session is already complete.");
            }

            var message = Message.CreateOutbound(
                conversation.Id,
                telegramMessageId,
                text,
                sentAt,
                DateTimeOffset.UtcNow);

            conversation.RegisterOutboundMessage(message.Text, message.SentAt);
            dbContext.Messages.Add(message);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            completed = true;

            var realtimeMessage = new RealtimeMessageDto(
                message.Id,
                message.Direction.ToString(),
                message.Text,
                message.SentAt,
                message.IsRead);

            return new(
                new OutgoingMessageDto(
                    message.Id,
                    conversation.Id,
                    message.Direction.ToString(),
                    message.Text,
                    message.SentAt,
                    message.IsRead),
                new MessageSentRealtimeDto(conversation.Id, realtimeMessage),
                new ConversationUpdatedRealtimeDto(
                    conversation.Id,
                    contact.Id,
                    ContactDisplayName.Build(contact),
                    contact.Username,
                    conversation.LastMessage!,
                    conversation.LastMessageAt!.Value,
                    conversation.UnreadCount));
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
