namespace EncodeLabs.Inbox.Application.Conversations;

public sealed record ConversationMessageDto(
    Guid Id,
    Guid ConversationId,
    string Direction,
    string Text,
    bool IsRead,
    DateTimeOffset SentAt);
