using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.OutgoingMessages;

public sealed class SendOutgoingMessageHandler(
    IOutgoingMessageStore store,
    ITelegramMessageSender telegramSender,
    IRealtimeNotifier realtimeNotifier)
{
    public const int MaximumTextLength = 4096;

    public async Task<SendOutgoingMessageResult> HandleAsync(
        Guid conversationId,
        string? text,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return new(SendOutgoingMessageStatus.NotFound);
        }

        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > MaximumTextLength)
        {
            return new(SendOutgoingMessageStatus.InvalidText);
        }

        var normalizedText = text.Trim();
        await using var session = await store.LockConversationAsync(
            conversationId,
            cancellationToken);

        if (session is null)
        {
            return new(SendOutgoingMessageStatus.NotFound);
        }

        var telegramResult = await telegramSender.SendTextMessageAsync(
            session.TelegramChatId,
            normalizedText,
            cancellationToken);

        if (!telegramResult.IsSuccess ||
            telegramResult.TelegramMessageId is null ||
            telegramResult.SentAt is null)
        {
            return new(
                SendOutgoingMessageStatus.TelegramFailed,
                TelegramFailure: telegramResult.Failure);
        }

        var persisted = await session.PersistAsync(
            telegramResult.TelegramMessageId.Value,
            normalizedText,
            telegramResult.SentAt.Value.ToUniversalTime(),
            cancellationToken);

        var realtimeStatus = RealtimeNotificationStatus.Sent;
        try
        {
            await realtimeNotifier.NotifyMessageSentAsync(
                persisted.MessageSent,
                cancellationToken);
            await realtimeNotifier.NotifyConversationUpdatedAsync(
                persisted.ConversationUpdated,
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // PostgreSQL has already committed. Realtime is best effort and must not
            // turn a successfully persisted Telegram message into an HTTP failure.
            realtimeStatus = RealtimeNotificationStatus.Failed;
        }

        return new(
            SendOutgoingMessageStatus.Sent,
            persisted.Message,
            realtimeStatus);
    }
}

public sealed record SendOutgoingMessageResult(
    SendOutgoingMessageStatus Status,
    OutgoingMessageDto? Message = null,
    RealtimeNotificationStatus RealtimeNotificationStatus = RealtimeNotificationStatus.NotApplicable,
    TelegramSendFailure TelegramFailure = TelegramSendFailure.None);

public enum SendOutgoingMessageStatus
{
    Sent = 1,
    NotFound = 2,
    InvalidText = 3,
    TelegramFailed = 4,
}
