using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.OutgoingMessages;

public interface IOutgoingMessageStore
{
    Task<IOutgoingMessageSession?> LockConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken);
}

public interface IOutgoingMessageSession : IAsyncDisposable
{
    long TelegramChatId { get; }

    Task<OutgoingMessagePersistenceResult> PersistAsync(
        long telegramMessageId,
        string text,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}

public sealed record OutgoingMessagePersistenceResult(
    OutgoingMessageDto Message,
    MessageSentRealtimeDto MessageSent,
    ConversationUpdatedRealtimeDto ConversationUpdated);
