using System.Text.Json.Serialization;

namespace EncodeLabs.Inbox.Application.Telegram;

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("chat")] TelegramChat? Chat,
    [property: JsonPropertyName("date")] long SentAtUnixSeconds,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id);

public sealed record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName);
