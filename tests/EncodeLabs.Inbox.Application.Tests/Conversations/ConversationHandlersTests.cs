using EncodeLabs.Inbox.Application.Conversations;
using EncodeLabs.Inbox.Application.Realtime;

namespace EncodeLabs.Inbox.Application.Tests.Conversations;

public sealed class ConversationHandlersTests
{
    private static readonly DateTimeOffset BaseTime =
        new(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Conversations_are_ordered_by_latest_activity_with_stable_tiebreaker()
    {
        var oldest = CreateSummary(BaseTime, 1, "Oldest");
        var tieFirst = CreateSummary(BaseTime.AddMinutes(1), 2, "Tie first");
        var tieSecond = CreateSummary(BaseTime.AddMinutes(1), 3, "Tie second");
        var store = new StubConversationStore
        {
            Conversations = [oldest, tieFirst, tieSecond],
        };

        var result = await new GetConversationsHandler(store).HandleAsync(CancellationToken.None);

        Assert.Equal([tieSecond.ConversationId, tieFirst.ConversationId, oldest.ConversationId],
            result.Select(conversation => conversation.ConversationId));
    }

    [Fact]
    public async Task Conversation_summary_preserves_preview_and_unread_count()
    {
        var expected = CreateSummary(BaseTime, 4, "Latest preview");
        var store = new StubConversationStore { Conversations = [expected] };

        var result = Assert.Single(
            await new GetConversationsHandler(store).HandleAsync(CancellationToken.None));

        Assert.Equal("Latest preview", result.LastMessageText);
        Assert.Equal(BaseTime, result.LastMessageAt);
        Assert.Equal(4, result.UnreadCount);
    }

    [Fact]
    public async Task Messages_are_returned_in_chronological_order_with_stable_tiebreaker()
    {
        var conversationId = Guid.NewGuid();
        var first = CreateMessage(conversationId, BaseTime, 1, "First");
        var tieFirst = CreateMessage(conversationId, BaseTime.AddMinutes(1), 2, "Tie first");
        var tieSecond = CreateMessage(conversationId, BaseTime.AddMinutes(1), 3, "Tie second");
        var store = new StubConversationStore
        {
            Messages = [tieSecond, tieFirst, first],
        };

        var result = await new GetConversationMessagesHandler(store)
            .HandleAsync(conversationId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal([first.Id, tieFirst.Id, tieSecond.Id], result.Select(message => message.Id));
    }

    [Fact]
    public async Task Missing_conversation_history_returns_not_found_result()
    {
        var store = new StubConversationStore { Messages = null };

        var result = await new GetConversationMessagesHandler(store)
            .HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Mark_as_read_notifies_only_after_persistence_and_in_consistent_order()
    {
        var events = new List<string>();
        var conversationId = Guid.NewGuid();
        var readMessageIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var store = new StubConversationStore
        {
            MarkAsRead = CreateReadPersistenceResult(conversationId, readMessageIds),
            Events = events,
        };
        var notifier = new RecordingNotifier(events);

        var result = await new MarkConversationAsReadHandler(store, notifier)
            .HandleAsync(conversationId, CancellationToken.None);

        Assert.True(result.Found);
        Assert.True(result.WasChanged);
        Assert.Equal(RealtimeNotificationStatus.Sent, result.RealtimeNotificationStatus);
        Assert.Equal(["commit", "MessagesRead", "ConversationUpdated"], events);
        Assert.Equal(readMessageIds, Assert.Single(notifier.MessagesRead).MessageIds);
        Assert.Equal(0, Assert.Single(notifier.Conversations).UnreadCount);
    }

    [Fact]
    public async Task Missing_conversation_does_not_notify()
    {
        var notifier = new RecordingNotifier([]);
        var store = new StubConversationStore { MarkAsRead = null };

        var result = await new MarkConversationAsReadHandler(store, notifier)
            .HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Found);
        Assert.Empty(notifier.MessagesRead);
        Assert.Empty(notifier.Conversations);
    }

    [Fact]
    public async Task Repeating_mark_as_read_is_idempotent_and_does_not_renotify()
    {
        var conversationId = Guid.NewGuid();
        var store = new SequencedConversationStore(
            CreateReadPersistenceResult(conversationId, [Guid.NewGuid()]),
            CreateReadPersistenceResult(conversationId, [], wasChanged: false));
        var notifier = new RecordingNotifier([]);
        var handler = new MarkConversationAsReadHandler(store, notifier);

        var first = await handler.HandleAsync(conversationId, CancellationToken.None);
        var second = await handler.HandleAsync(conversationId, CancellationToken.None);

        Assert.True(first.WasChanged);
        Assert.False(second.WasChanged);
        Assert.Single(notifier.MessagesRead);
        Assert.Single(notifier.Conversations);
    }

    [Fact]
    public async Task Persistence_failure_does_not_notify()
    {
        var notifier = new RecordingNotifier([]);
        var handler = new MarkConversationAsReadHandler(new FailingConversationStore(), notifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Empty(notifier.MessagesRead);
        Assert.Empty(notifier.Conversations);
    }

    [Fact]
    public async Task Realtime_failure_after_commit_does_not_fail_mark_as_read()
    {
        var conversationId = Guid.NewGuid();
        var handler = new MarkConversationAsReadHandler(
            new StubConversationStore
            {
                MarkAsRead = CreateReadPersistenceResult(conversationId, [Guid.NewGuid()]),
            },
            new RecordingNotifier([], failMessagesRead: true));

        var result = await handler.HandleAsync(conversationId, CancellationToken.None);

        Assert.True(result.Found);
        Assert.True(result.WasChanged);
        Assert.Equal(RealtimeNotificationStatus.Failed, result.RealtimeNotificationStatus);
    }

    private static ConversationSummaryDto CreateSummary(
        DateTimeOffset lastMessageAt,
        int unreadCount,
        string text) =>
        new(
            GuidFrom(unreadCount),
            Guid.NewGuid(),
            "Ada Lovelace",
            "ada",
            text,
            lastMessageAt,
            unreadCount);

    private static ConversationMessageDto CreateMessage(
        Guid conversationId,
        DateTimeOffset sentAt,
        int idSeed,
        string text) =>
        new(GuidFrom(idSeed), conversationId, "Inbound", text, false, sentAt);

    private static MarkConversationAsReadPersistenceResult CreateReadPersistenceResult(
        Guid conversationId,
        IReadOnlyList<Guid> messageIds,
        bool wasChanged = true) =>
        new(
            wasChanged,
            messageIds,
            new ConversationUpdatedRealtimeDto(
                conversationId,
                Guid.NewGuid(),
                "Ada Lovelace",
                "ada",
                "Latest",
                BaseTime,
                0));

    private static Guid GuidFrom(int value) =>
        new($"00000000-0000-0000-0000-{value:D12}");

    private class StubConversationStore : IConversationStore
    {
        public IReadOnlyList<ConversationSummaryDto> Conversations { get; init; } = [];

        public IReadOnlyList<ConversationMessageDto>? Messages { get; init; } = [];

        public MarkConversationAsReadPersistenceResult? MarkAsRead { get; init; }

        public List<string>? Events { get; init; }

        public Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Conversations);

        public Task<IReadOnlyList<ConversationMessageDto>?> GetMessagesAsync(
            Guid conversationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Messages);

        public virtual Task<MarkConversationAsReadPersistenceResult?> MarkAsReadAsync(
            Guid conversationId,
            CancellationToken cancellationToken)
        {
            Events?.Add("commit");
            return Task.FromResult(MarkAsRead);
        }
    }

    private sealed class SequencedConversationStore(
        params MarkConversationAsReadPersistenceResult[] results) : StubConversationStore
    {
        private int index;

        public override Task<MarkConversationAsReadPersistenceResult?> MarkAsReadAsync(
            Guid conversationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<MarkConversationAsReadPersistenceResult?>(results[index++]);
    }

    private sealed class FailingConversationStore : StubConversationStore
    {
        public override Task<MarkConversationAsReadPersistenceResult?> MarkAsReadAsync(
            Guid conversationId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Database failure");
    }

    private sealed class RecordingNotifier(
        List<string> events,
        bool failMessagesRead = false) : IRealtimeNotifier
    {
        public List<MessagesReadRealtimeDto> MessagesRead { get; } = [];

        public List<ConversationUpdatedRealtimeDto> Conversations { get; } = [];

        public Task NotifyMessageReceivedAsync(
            MessageReceivedRealtimeDto message,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyMessageSentAsync(
            MessageSentRealtimeDto message,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyConversationUpdatedAsync(
            ConversationUpdatedRealtimeDto conversation,
            CancellationToken cancellationToken)
        {
            events.Add("ConversationUpdated");
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task NotifyMessagesReadAsync(
            MessagesReadRealtimeDto messagesRead,
            CancellationToken cancellationToken)
        {
            events.Add("MessagesRead");
            if (failMessagesRead)
            {
                throw new InvalidOperationException("SignalR failure");
            }

            MessagesRead.Add(messagesRead);
            return Task.CompletedTask;
        }
    }
}
