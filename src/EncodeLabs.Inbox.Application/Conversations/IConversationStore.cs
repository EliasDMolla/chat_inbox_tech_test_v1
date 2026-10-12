using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.Conversations;

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConversationMessageDto>?> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<MarkConversationAsReadPersistenceResult?> MarkAsReadAsync(
        Guid conversationId,
        CancellationToken cancellationToken);
}

public sealed record MarkConversationAsReadPersistenceResult(
    bool WasChanged,
    IReadOnlyList<Guid> ReadMessageIds,
    ConversationUpdatedRealtimeDto Conversation);
