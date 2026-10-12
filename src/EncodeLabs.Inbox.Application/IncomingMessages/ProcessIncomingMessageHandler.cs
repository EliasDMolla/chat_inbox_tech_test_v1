using EncodeLabs.Inbox.Application.Telegram;
using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.IncomingMessages;

public sealed class ProcessIncomingMessageHandler(
    IIncomingMessageStore store,
    IRealtimeNotifier realtimeNotifier)
{
    public async Task<IncomingMessageProcessingResult> HandleAsync(
        IncomingTelegramMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.UpdateId <= 0 ||
            message.MessageId <= 0 ||
            message.ChatId == 0 ||
            message.TelegramUserId <= 0 ||
            string.IsNullOrWhiteSpace(message.Text))
        {
            throw new InvalidIncomingMessageException(
                "The incoming Telegram message does not contain all required fields.");
        }

        if (message.SentAt == default)
        {
            throw new InvalidIncomingMessageException(
                "The incoming Telegram message has an invalid sent date.");
        }

        var result = await store.StoreAsync(message, cancellationToken);

        if (result.Status == IncomingMessageProcessingStatus.Duplicate)
        {
            return result;
        }

        if (result.MessageReceived is null || result.ConversationUpdated is null)
        {
            return result with
            {
                RealtimeNotificationStatus = RealtimeNotificationStatus.Failed,
            };
        }

        try
        {
            await realtimeNotifier.NotifyMessageReceivedAsync(
                result.MessageReceived,
                cancellationToken);
            await realtimeNotifier.NotifyConversationUpdatedAsync(
                result.ConversationUpdated,
                cancellationToken);

            return result with
            {
                RealtimeNotificationStatus = RealtimeNotificationStatus.Sent,
            };
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Persistence has already committed. PostgreSQL remains the source of truth,
            // so a realtime failure must not cause the domain operation to be retried.
            return result with
            {
                RealtimeNotificationStatus = RealtimeNotificationStatus.Failed,
            };
        }
    }
}
