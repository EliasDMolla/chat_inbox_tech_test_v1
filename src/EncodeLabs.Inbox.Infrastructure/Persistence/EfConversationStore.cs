using EncodeLabs.Inbox.Application.Conversations;
using EncodeLabs.Inbox.Application.Realtime;
using EncodeLabs.Inbox.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

internal sealed class EfConversationStore(AppDbContext dbContext) : IConversationStore
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await (
            from conversation in dbContext.Conversations.AsNoTracking()
            join contact in dbContext.Contacts.AsNoTracking()
                on conversation.ContactId equals contact.Id
            where conversation.LastMessage != null && conversation.LastMessageAt != null
            orderby conversation.LastMessageAt descending, conversation.Id descending
            select new ConversationRow(
                conversation.Id,
                contact.Id,
                contact.TelegramUserId,
                contact.Username,
                contact.FirstName,
                contact.LastName,
                conversation.LastMessage!,
                conversation.LastMessageAt!.Value,
                conversation.UnreadCount))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ConversationSummaryDto(
                row.ConversationId,
                row.ContactId,
                ContactDisplayName.Build(
                    row.FirstName,
                    row.LastName,
                    row.Username,
                    row.TelegramUserId),
                row.Username,
                row.LastMessageText,
                row.LastMessageAt,
                row.UnreadCount))
            .ToArray();
    }

    public async Task<IReadOnlyList<ConversationMessageDto>?> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Conversations.AsNoTracking().AnyAsync(
                conversation => conversation.Id == conversationId,
                cancellationToken))
        {
            return null;
        }

        return await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .Select(message => new ConversationMessageDto(
                message.Id,
                message.ConversationId,
                message.Direction.ToString(),
                message.Text,
                message.IsRead,
                message.SentAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<MarkConversationAsReadPersistenceResult?> MarkAsReadAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            candidate => candidate.Id == conversationId,
            cancellationToken);

        if (conversation is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await AcquireChatLockAsync(conversation.TelegramChatId, cancellationToken);
        await dbContext.Entry(conversation).ReloadAsync(cancellationToken);

        var contact = await dbContext.Contacts.AsNoTracking().SingleAsync(
            candidate => candidate.Id == conversation.ContactId,
            cancellationToken);
        var unreadMessages = await dbContext.Messages
            .Where(message =>
                message.ConversationId == conversationId &&
                message.Direction == MessageDirection.Inbound &&
                !message.IsRead)
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);

        var readMessageIds = new List<Guid>(unreadMessages.Count);
        foreach (var message in unreadMessages)
        {
            if (message.MarkAsRead())
            {
                readMessageIds.Add(message.Id);
            }
        }

        var conversationChanged = conversation.MarkAsRead();
        var wasChanged = conversationChanged || readMessageIds.Count > 0;

        if (wasChanged)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var conversationUpdated = new ConversationUpdatedRealtimeDto(
            conversation.Id,
            contact.Id,
            ContactDisplayName.Build(
                contact.FirstName,
                contact.LastName,
                contact.Username,
                contact.TelegramUserId),
            contact.Username,
            conversation.LastMessage!,
            conversation.LastMessageAt!.Value,
            conversation.UnreadCount);

        return new(wasChanged, readMessageIds, conversationUpdated);
    }

    private Task AcquireChatLockAsync(long telegramChatId, CancellationToken cancellationToken)
    {
        var lockKey = $"chat:{telegramChatId}";
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
    }

    private sealed record ConversationRow(
        Guid ConversationId,
        Guid ContactId,
        long TelegramUserId,
        string? Username,
        string? FirstName,
        string? LastName,
        string LastMessageText,
        DateTimeOffset LastMessageAt,
        int UnreadCount);
}
