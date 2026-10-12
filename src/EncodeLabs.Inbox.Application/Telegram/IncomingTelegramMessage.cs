namespace EncodeLabs.Inbox.Application.Telegram;

public sealed record IncomingTelegramMessage(
    long UpdateId,
    long MessageId,
    long ChatId,
    long TelegramUserId,
    string? Username,
    string? FirstName,
    string? LastName,
    string Text,
    DateTimeOffset SentAt);
