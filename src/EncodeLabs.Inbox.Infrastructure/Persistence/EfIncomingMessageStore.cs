using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Application.Realtime;
using EncodeLabs.Inbox.Application.Telegram;
using EncodeLabs.Inbox.Domain.Contacts;
using EncodeLabs.Inbox.Domain.Conversations;
using EncodeLabs.Inbox.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

internal sealed class EfIncomingMessageStore(AppDbContext dbContext) : IIncomingMessageStore
{
    private const string TelegramUpdateConstraint = "ux_messages_telegram_update_id";

    public async Task<IncomingMessageProcessingResult> StoreAsync(
        IncomingTelegramMessage incoming,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await AcquireLockAsync($"contact:{incoming.TelegramUserId}", cancellationToken);
            await AcquireLockAsync($"chat:{incoming.ChatId}", cancellationToken);

            if (await dbContext.Messages.AnyAsync(
                    message => message.TelegramUpdateId == incoming.UpdateId,
                    cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return new(IncomingMessageProcessingStatus.Duplicate);
            }

            var now = DateTimeOffset.UtcNow;
            var contact = await dbContext.Contacts.SingleOrDefaultAsync(
                candidate => candidate.TelegramUserId == incoming.TelegramUserId,
                cancellationToken);

            if (contact is null)
            {
                contact = new Contact(
                    incoming.TelegramUserId,
                    incoming.Username,
                    incoming.FirstName,
                    incoming.LastName,
                    now);
                dbContext.Contacts.Add(contact);
            }
            else
            {
                contact.UpdateProfile(incoming.Username, incoming.FirstName, incoming.LastName);
            }

            var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
                candidate => candidate.TelegramChatId == incoming.ChatId,
                cancellationToken);

            if (conversation is null)
            {
                conversation = new Conversation(contact.Id, incoming.ChatId, now);
                dbContext.Conversations.Add(conversation);
            }

            var message = Message.CreateInbound(
                conversation.Id,
                incoming.MessageId,
                incoming.UpdateId,
                incoming.Text,
                incoming.SentAt.ToUniversalTime(),
                now);

            conversation.RegisterInboundMessage(message.Text, message.SentAt);
            dbContext.Messages.Add(message);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var messageReceived = new MessageReceivedRealtimeDto(
                conversation.Id,
                new RealtimeMessageDto(
                    message.Id,
                    message.Direction.ToString(),
                    message.Text,
                    message.SentAt,
                    message.IsRead));
            var conversationUpdated = new ConversationUpdatedRealtimeDto(
                conversation.Id,
                contact.Id,
                BuildDisplayName(contact),
                contact.Username,
                conversation.LastMessage!,
                conversation.LastMessageAt!.Value,
                conversation.UnreadCount);

            return new(
                IncomingMessageProcessingStatus.Processed,
                contact.Id,
                conversation.Id,
                message.Id,
                messageReceived,
                conversationUpdated);
        }
        catch (DbUpdateException exception) when (IsDuplicateUpdate(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return new(IncomingMessageProcessingStatus.Duplicate);
        }
    }

    private Task AcquireLockAsync(string key, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
            cancellationToken);

    private static bool IsDuplicateUpdate(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: TelegramUpdateConstraint,
        };

    private static string BuildDisplayName(Contact contact)
    {
        var fullName = string.Join(
            ' ',
            new[] { contact.FirstName, contact.LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (!string.IsNullOrEmpty(fullName))
        {
            return fullName;
        }

        return contact.Username ?? contact.TelegramUserId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
