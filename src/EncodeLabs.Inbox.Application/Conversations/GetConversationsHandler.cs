namespace EncodeLabs.Inbox.Application.Conversations;

public sealed class GetConversationsHandler(IConversationStore store)
{
    public async Task<IReadOnlyList<ConversationSummaryDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        var conversations = await store.GetConversationsAsync(cancellationToken);

        return conversations
            .OrderByDescending(conversation => conversation.LastMessageAt)
            .ThenByDescending(conversation => conversation.ConversationId)
            .ToArray();
    }
}
