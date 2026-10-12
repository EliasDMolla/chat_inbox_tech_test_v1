namespace EncodeLabs.Inbox.Application.Realtime;

public sealed record ConversationUpdatedRealtimeDto(
    Guid ConversationId,
    Guid ContactId,
    string DisplayName,
    string? Username,
    string LastMessageText,
    DateTimeOffset LastMessageAt,
    int UnreadCount);
