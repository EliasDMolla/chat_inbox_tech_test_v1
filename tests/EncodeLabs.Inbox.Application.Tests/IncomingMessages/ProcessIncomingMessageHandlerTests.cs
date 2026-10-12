using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Application.Realtime;
using EncodeLabs.Inbox.Application.Telegram;

namespace EncodeLabs.Inbox.Application.Tests.IncomingMessages;

public sealed class ProcessIncomingMessageHandlerTests
{
    private static readonly DateTimeOffset SentAt = new(2026, 10, 12, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task New_message_is_persisted_and_notified_after_store_completes()
    {
        var resultToStore = CreateProcessedResult();
        var store = new RecordingStore(resultToStore);
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(store, notifier);
        var message = CreateMessage();

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(IncomingMessageProcessingStatus.Processed, result.Status);
        Assert.Equal(RealtimeNotificationStatus.Sent, result.RealtimeNotificationStatus);
        Assert.Same(message, Assert.Single(store.Messages));
        Assert.Equal(
            ["store", "MessageReceived", "ConversationUpdated"],
            store.Events.Concat(notifier.Events));
    }

    [Fact]
    public async Task Message_received_dto_contains_expected_message_data()
    {
        var processed = CreateProcessedResult();
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(new RecordingStore(processed), notifier);

        await handler.HandleAsync(CreateMessage(), CancellationToken.None);

        var notification = Assert.Single(notifier.Messages);
        Assert.Equal(processed.ConversationId, notification.ConversationId);
        Assert.Equal(processed.MessageId, notification.Message.Id);
        Assert.Equal("Inbound", notification.Message.Direction);
        Assert.Equal("Hello", notification.Message.Text);
        Assert.Equal(SentAt, notification.Message.SentAt);
        Assert.False(notification.Message.IsRead);
    }

    [Fact]
    public async Task Conversation_updated_dto_contains_latest_summary()
    {
        var processed = CreateProcessedResult();
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(new RecordingStore(processed), notifier);

        await handler.HandleAsync(CreateMessage(), CancellationToken.None);

        var notification = Assert.Single(notifier.Conversations);
        Assert.Equal(processed.ConversationId, notification.ConversationId);
        Assert.Equal(processed.ContactId, notification.ContactId);
        Assert.Equal("Ada Lovelace", notification.DisplayName);
        Assert.Equal("ada", notification.Username);
        Assert.Equal("Hello", notification.LastMessageText);
        Assert.Equal(SentAt, notification.LastMessageAt);
        Assert.Equal(2, notification.UnreadCount);
    }

    [Fact]
    public async Task Duplicate_result_does_not_emit_realtime_notifications()
    {
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(
            new RecordingStore(new(IncomingMessageProcessingStatus.Duplicate)),
            notifier);

        var result = await handler.HandleAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal(IncomingMessageProcessingStatus.Duplicate, result.Status);
        Assert.Equal(RealtimeNotificationStatus.NotApplicable, result.RealtimeNotificationStatus);
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task Persistence_failure_does_not_emit_realtime_notifications()
    {
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(new FailingStore(), notifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(CreateMessage(), CancellationToken.None));

        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task Realtime_failure_after_commit_is_reported_without_failing_processing()
    {
        var handler = new ProcessIncomingMessageHandler(
            new RecordingStore(CreateProcessedResult()),
            new RecordingNotifier(failMessageReceived: true));

        var result = await handler.HandleAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal(IncomingMessageProcessingStatus.Processed, result.Status);
        Assert.Equal(RealtimeNotificationStatus.Failed, result.RealtimeNotificationStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_text_is_rejected_before_persistence(string text)
    {
        var store = new RecordingStore(CreateProcessedResult());
        var notifier = new RecordingNotifier();
        var handler = new ProcessIncomingMessageHandler(store, notifier);

        await Assert.ThrowsAsync<InvalidIncomingMessageException>(() =>
            handler.HandleAsync(CreateMessage(text), CancellationToken.None));
        Assert.Empty(store.Messages);
        Assert.Empty(notifier.Events);
    }

    private static IncomingMessageProcessingResult CreateProcessedResult()
    {
        var contactId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        return new(
            IncomingMessageProcessingStatus.Processed,
            contactId,
            conversationId,
            messageId,
            new MessageReceivedRealtimeDto(
                conversationId,
                new RealtimeMessageDto(
                    messageId,
                    "Inbound",
                    "Hello",
                    SentAt,
                    false)),
            new ConversationUpdatedRealtimeDto(
                conversationId,
                contactId,
                "Ada Lovelace",
                "ada",
                "Hello",
                SentAt,
                2));
    }

    private static IncomingTelegramMessage CreateMessage(string text = "Hello") =>
        new(1, 2, 3, 4, "ada", "Ada", "Lovelace", text, SentAt);

    private sealed class RecordingStore(IncomingMessageProcessingResult result)
        : IIncomingMessageStore
    {
        public List<IncomingTelegramMessage> Messages { get; } = [];

        public List<string> Events { get; } = [];

        public Task<IncomingMessageProcessingResult> StoreAsync(
            IncomingTelegramMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            Events.Add("store");
            return Task.FromResult(result);
        }
    }

    private sealed class FailingStore : IIncomingMessageStore
    {
        public Task<IncomingMessageProcessingResult> StoreAsync(
            IncomingTelegramMessage message,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Database failure");
    }

    private sealed class RecordingNotifier(bool failMessageReceived = false)
        : IRealtimeNotifier
    {
        public List<string> Events { get; } = [];

        public List<MessageReceivedRealtimeDto> Messages { get; } = [];

        public List<ConversationUpdatedRealtimeDto> Conversations { get; } = [];

        public Task NotifyMessageReceivedAsync(
            MessageReceivedRealtimeDto message,
            CancellationToken cancellationToken)
        {
            Events.Add("MessageReceived");
            if (failMessageReceived)
            {
                throw new InvalidOperationException("SignalR failure");
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task NotifyMessageSentAsync(
            MessageSentRealtimeDto message,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyConversationUpdatedAsync(
            ConversationUpdatedRealtimeDto conversation,
            CancellationToken cancellationToken)
        {
            Events.Add("ConversationUpdated");
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task NotifyMessagesReadAsync(
            MessagesReadRealtimeDto messagesRead,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
