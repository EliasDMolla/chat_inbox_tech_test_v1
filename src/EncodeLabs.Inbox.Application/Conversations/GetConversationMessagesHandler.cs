namespace EncodeLabs.Inbox.Application.Conversations;

public sealed class GetConversationMessagesHandler(IConversationStore store)
{
    public async Task<IReadOnlyList<ConversationMessageDto>?> HandleAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return null;
        }

        var messages = await store.GetMessagesAsync(conversationId, cancellationToken);

        return messages?
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToArray();
    }
}
