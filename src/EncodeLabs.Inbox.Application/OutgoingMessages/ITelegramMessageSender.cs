namespace EncodeLabs.Inbox.Application.OutgoingMessages;

public interface ITelegramMessageSender
{
    Task<TelegramSendResult> SendTextMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken);
}

public sealed record TelegramSendResult(
    bool IsSuccess,
    long? TelegramMessageId = null,
    DateTimeOffset? SentAt = null,
    TelegramSendFailure Failure = TelegramSendFailure.None)
{
    public static TelegramSendResult Success(long telegramMessageId, DateTimeOffset sentAt) =>
        new(true, telegramMessageId, sentAt);

    public static TelegramSendResult Failed(TelegramSendFailure failure) =>
        new(false, Failure: failure);
}

public enum TelegramSendFailure
{
    None = 0,
    Rejected = 1,
    Unavailable = 2,
    NotConfigured = 3,
}
