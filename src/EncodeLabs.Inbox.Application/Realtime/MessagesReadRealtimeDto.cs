namespace EncodeLabs.Inbox.Application.Realtime;

public sealed record MessagesReadRealtimeDto(
    Guid ConversationId,
    IReadOnlyList<Guid> MessageIds);
