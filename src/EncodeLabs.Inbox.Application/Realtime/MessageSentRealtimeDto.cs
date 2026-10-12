namespace EncodeLabs.Inbox.Application.Realtime;

public sealed record MessageSentRealtimeDto(
    Guid ConversationId,
    RealtimeMessageDto Message);
