using EncodeLabs.Inbox.Application.OutgoingMessages;
using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.Tests.OutgoingMessages;

public sealed class SendOutgoingMessageHandlerTests
{
    private static readonly Guid ConversationId = Guid.NewGuid();
    private static readonly Guid MessageId = Guid.NewGuid();
    private static readonly DateTimeOffset SentAt =
        new(2026, 10, 12, 19, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_conversation_returns_not_found_without_calling_telegram()
    {
        var sender = new RecordingSender(TelegramSendResult.Success(50, SentAt));
        var handler = new SendOutgoingMessageHandler(
            new RecordingStore(null),
            sender,
            new RecordingNotifier([]));

        var result = await handler.HandleAsync(ConversationId, "Hello", CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.NotFound, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_text_is_rejected_before_locking(string? text)
    {
        var store = new RecordingStore(new RecordingSession([]));
        var handler = new SendOutgoingMessageHandler(
            store,
            new RecordingSender(TelegramSendResult.Success(50, SentAt)),
            new RecordingNotifier([]));

        var result = await handler.HandleAsync(ConversationId, text, CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.InvalidText, result.Status);
        Assert.Equal(0, store.LockCalls);
    }

    [Fact]
    public async Task Text_longer_than_telegram_limit_is_rejected_before_locking()
    {
        var store = new RecordingStore(new RecordingSession([]));
        var handler = new SendOutgoingMessageHandler(
            store,
            new RecordingSender(TelegramSendResult.Success(50, SentAt)),
            new RecordingNotifier([]));

        var result = await handler.HandleAsync(
            ConversationId,
            new string('a', SendOutgoingMessageHandler.MaximumTextLength + 1),
            CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.InvalidText, result.Status);
        Assert.Equal(0, store.LockCalls);
    }

    [Fact]
    public async Task Successful_send_persists_outbound_and_notifies_after_commit()
    {
        var events = new List<string>();
        var session = new RecordingSession(events);
        var notifier = new RecordingNotifier(events);
        var handler = new SendOutgoingMessageHandler(
            new RecordingStore(session, events),
            new RecordingSender(TelegramSendResult.Success(50, SentAt), events),
            notifier);

        var result = await handler.HandleAsync(ConversationId, " Reply ", CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.Sent, result.Status);
        Assert.NotNull(result.Message);
        Assert.Equal("Outbound", result.Message.Direction);
        Assert.Equal("Reply", result.Message.Text);
        Assert.True(result.Message.IsRead);
        Assert.Equal(SentAt, result.Message.SentAt);
        Assert.Equal(50, session.TelegramMessageId);
        Assert.Equal("Reply", session.Text);
        Assert.Equal(
            ["lock", "telegram", "commit", "MessageSent", "ConversationUpdated"],
            events);
        Assert.Equal(3, Assert.Single(notifier.Conversations).UnreadCount);
    }

    [Fact]
    public async Task Telegram_failure_does_not_persist_or_notify()
    {
        var events = new List<string>();
        var session = new RecordingSession(events);
        var notifier = new RecordingNotifier(events);
        var handler = new SendOutgoingMessageHandler(
            new RecordingStore(session, events),
            new RecordingSender(TelegramSendResult.Failed(TelegramSendFailure.Rejected), events),
            notifier);

        var result = await handler.HandleAsync(ConversationId, "Hello", CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.TelegramFailed, result.Status);
        Assert.Equal(TelegramSendFailure.Rejected, result.TelegramFailure);
        Assert.Equal(0, session.PersistCalls);
        Assert.Empty(notifier.Events);
        Assert.Equal(["lock", "telegram"], events);
    }

    [Fact]
    public async Task Persistence_failure_does_not_emit_realtime()
    {
        var events = new List<string>();
        var notifier = new RecordingNotifier(events);
        var handler = new SendOutgoingMessageHandler(
            new RecordingStore(new RecordingSession(events, failPersistence: true), events),
            new RecordingSender(TelegramSendResult.Success(50, SentAt), events),
            notifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(ConversationId, "Hello", CancellationToken.None));

        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task Realtime_failure_after_commit_keeps_operation_successful()
    {
        var handler = new SendOutgoingMessageHandler(
            new RecordingStore(new RecordingSession([])),
            new RecordingSender(TelegramSendResult.Success(50, SentAt)),
            new RecordingNotifier([], failMessageSent: true));

        var result = await handler.HandleAsync(ConversationId, "Hello", CancellationToken.None);

        Assert.Equal(SendOutgoingMessageStatus.Sent, result.Status);
        Assert.Equal(RealtimeNotificationStatus.Failed, result.RealtimeNotificationStatus);
        Assert.NotNull(result.Message);
    }

    private static OutgoingMessagePersistenceResult CreatePersistenceResult() =>
        new(
            new OutgoingMessageDto(
                MessageId,
                ConversationId,
                "Outbound",
                "Reply",
                SentAt,
                true),
            new MessageSentRealtimeDto(
                ConversationId,
                new RealtimeMessageDto(MessageId, "Outbound", "Reply", SentAt, true)),
            new ConversationUpdatedRealtimeDto(
                ConversationId,
                Guid.NewGuid(),
                "Ada Lovelace",
                "ada",
                "Reply",
                SentAt,
                3));

    private sealed class RecordingStore(
        RecordingSession? session,
        List<string>? events = null) : IOutgoingMessageStore
    {
        public int LockCalls { get; private set; }

        public Task<IOutgoingMessageSession?> LockConversationAsync(
            Guid conversationId,
            CancellationToken cancellationToken)
        {
            LockCalls++;
            events?.Add("lock");
            return Task.FromResult<IOutgoingMessageSession?>(session);
        }
    }

    private sealed class RecordingSession(
        List<string> events,
        bool failPersistence = false) : IOutgoingMessageSession
    {
        public long TelegramChatId => 123;

        public int PersistCalls { get; private set; }

        public long? TelegramMessageId { get; private set; }

        public string? Text { get; private set; }

        public Task<OutgoingMessagePersistenceResult> PersistAsync(
            long telegramMessageId,
            string text,
            DateTimeOffset sentAt,
            CancellationToken cancellationToken)
        {
            PersistCalls++;
            TelegramMessageId = telegramMessageId;
            Text = text;
            events.Add("commit");
            return failPersistence
                ? throw new InvalidOperationException("Database failure")
                : Task.FromResult(CreatePersistenceResult());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingSender(
        TelegramSendResult result,
        List<string>? events = null) : ITelegramMessageSender
    {
        public List<(long ChatId, string Text)> Messages { get; } = [];

        public Task<TelegramSendResult> SendTextMessageAsync(
            long chatId,
            string text,
            CancellationToken cancellationToken)
        {
            Messages.Add((chatId, text));
            events?.Add("telegram");
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingNotifier(
        List<string> events,
        bool failMessageSent = false) : IRealtimeNotifier
    {
        public List<string> Events { get; } = [];

        public List<ConversationUpdatedRealtimeDto> Conversations { get; } = [];

        public Task NotifyMessageReceivedAsync(
            MessageReceivedRealtimeDto message,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyMessageSentAsync(
            MessageSentRealtimeDto message,
            CancellationToken cancellationToken)
        {
            Events.Add("MessageSent");
            events.Add("MessageSent");
            return failMessageSent
                ? throw new InvalidOperationException("SignalR failure")
                : Task.CompletedTask;
        }

        public Task NotifyConversationUpdatedAsync(
            ConversationUpdatedRealtimeDto conversation,
            CancellationToken cancellationToken)
        {
            Events.Add("ConversationUpdated");
            events.Add("ConversationUpdated");
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task NotifyMessagesReadAsync(
            MessagesReadRealtimeDto messagesRead,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
