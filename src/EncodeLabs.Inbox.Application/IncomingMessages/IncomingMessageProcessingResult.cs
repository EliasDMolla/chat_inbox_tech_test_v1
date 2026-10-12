using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.IncomingMessages;

public sealed record IncomingMessageProcessingResult(
    IncomingMessageProcessingStatus Status,
    Guid? ContactId = null,
    Guid? ConversationId = null,
    Guid? MessageId = null,
    MessageReceivedRealtimeDto? MessageReceived = null,
    ConversationUpdatedRealtimeDto? ConversationUpdated = null,
    RealtimeNotificationStatus RealtimeNotificationStatus =
        RealtimeNotificationStatus.NotApplicable);

public enum IncomingMessageProcessingStatus
{
    Processed,
    Duplicate,
}
