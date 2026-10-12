namespace EncodeLabs.Inbox.Application.Realtime;

public interface IRealtimeNotifier
{
    Task NotifyMessageReceivedAsync(
        MessageReceivedRealtimeDto message,
        CancellationToken cancellationToken);

    Task NotifyMessageSentAsync(
        MessageSentRealtimeDto message,
        CancellationToken cancellationToken);

    Task NotifyConversationUpdatedAsync(
        ConversationUpdatedRealtimeDto conversation,
        CancellationToken cancellationToken);

    Task NotifyMessagesReadAsync(
        MessagesReadRealtimeDto messagesRead,
        CancellationToken cancellationToken);
}
