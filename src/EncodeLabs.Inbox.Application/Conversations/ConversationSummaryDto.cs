namespace EncodeLabs.Inbox.Application.Conversations;

public sealed record ConversationSummaryDto(
    Guid ConversationId,
    Guid ContactId,
    string DisplayName,
    string? Username,
    string LastMessageText,
    DateTimeOffset LastMessageAt,
    int UnreadCount);
