using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.Conversations;

public sealed class MarkConversationAsReadHandler(
    IConversationStore store,
    IRealtimeNotifier realtimeNotifier)
{
    public async Task<MarkConversationAsReadResult> HandleAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return new(false, false, RealtimeNotificationStatus.NotApplicable);
        }

        var persisted = await store.MarkAsReadAsync(conversationId, cancellationToken);

        if (persisted is null)
        {
            return new(false, false, RealtimeNotificationStatus.NotApplicable);
        }

        if (!persisted.WasChanged)
        {
            return new(true, false, RealtimeNotificationStatus.NotApplicable);
        }

        try
        {
            if (persisted.ReadMessageIds.Count > 0)
            {
                await realtimeNotifier.NotifyMessagesReadAsync(
                    new MessagesReadRealtimeDto(conversationId, persisted.ReadMessageIds),
                    cancellationToken);
            }

            await realtimeNotifier.NotifyConversationUpdatedAsync(
                persisted.Conversation,
                cancellationToken);

            return new(true, true, RealtimeNotificationStatus.Sent);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The read state has already committed. Realtime is best effort and must
            // not change the successful HTTP outcome or PostgreSQL state.
            return new(true, true, RealtimeNotificationStatus.Failed);
        }
    }
}

public sealed record MarkConversationAsReadResult(
    bool Found,
    bool WasChanged,
    RealtimeNotificationStatus RealtimeNotificationStatus);
