namespace EncodeLabs.Inbox.Application.Realtime;

public sealed record MessageReceivedRealtimeDto(
    Guid ConversationId,
    RealtimeMessageDto Message);

public sealed record RealtimeMessageDto(
    Guid Id,
    string Direction,
    string Text,
    DateTimeOffset SentAt,
    bool IsRead);
