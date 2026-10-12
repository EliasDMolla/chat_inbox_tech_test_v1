namespace EncodeLabs.Inbox.Application.IncomingMessages;

public sealed record IncomingMessageProcessingResult(
    IncomingMessageProcessingStatus Status,
    Guid? ContactId = null,
    Guid? ConversationId = null,
    Guid? MessageId = null);

public enum IncomingMessageProcessingStatus
{
    Processed,
    Duplicate,
}
