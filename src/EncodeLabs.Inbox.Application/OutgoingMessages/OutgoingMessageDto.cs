namespace EncodeLabs.Inbox.Application.OutgoingMessages;

public sealed record OutgoingMessageDto(
    Guid Id,
    Guid ConversationId,
    string Direction,
    string Text,
    DateTimeOffset SentAt,
    bool IsRead);
