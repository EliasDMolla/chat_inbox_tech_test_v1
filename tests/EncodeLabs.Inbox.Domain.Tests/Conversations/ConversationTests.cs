using EncodeLabs.Inbox.Domain.Conversations;

namespace EncodeLabs.Inbox.Domain.Tests.Conversations;

public sealed class ConversationTests
{
    [Fact]
    public void Inbound_messages_increment_unread_count_and_keep_latest_message()
    {
        var createdAt = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var conversation = new Conversation(Guid.NewGuid(), 123, createdAt);

        conversation.RegisterInboundMessage("First", createdAt.AddMinutes(1));
        conversation.RegisterInboundMessage("Second", createdAt.AddMinutes(2));

        Assert.Equal(2, conversation.UnreadCount);
        Assert.Equal("Second", conversation.LastMessage);
        Assert.Equal(createdAt.AddMinutes(2), conversation.LastMessageAt);
    }

    [Fact]
    public void Older_message_increments_unread_without_replacing_latest_message()
    {
        var createdAt = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var conversation = new Conversation(Guid.NewGuid(), 123, createdAt);
        conversation.RegisterInboundMessage("Latest", createdAt.AddMinutes(2));

        conversation.RegisterInboundMessage("Delayed", createdAt.AddMinutes(1));

        Assert.Equal(2, conversation.UnreadCount);
        Assert.Equal("Latest", conversation.LastMessage);
        Assert.Equal(createdAt.AddMinutes(2), conversation.LastMessageAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_message_does_not_change_conversation(string text)
    {
        var conversation = new Conversation(Guid.NewGuid(), 123, DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            conversation.RegisterInboundMessage(text, DateTimeOffset.UtcNow));
        Assert.Equal(0, conversation.UnreadCount);
        Assert.Null(conversation.LastMessage);
        Assert.Null(conversation.LastMessageAt);
    }

    [Fact]
    public void Mark_as_read_resets_unread_count_idempotently()
    {
        var conversation = new Conversation(Guid.NewGuid(), 123, DateTimeOffset.UtcNow);
        conversation.RegisterInboundMessage("First", DateTimeOffset.UtcNow);
        conversation.RegisterInboundMessage("Second", DateTimeOffset.UtcNow);

        var firstChange = conversation.MarkAsRead();
        var secondChange = conversation.MarkAsRead();

        Assert.True(firstChange);
        Assert.False(secondChange);
        Assert.Equal(0, conversation.UnreadCount);
    }

    [Fact]
    public void Outbound_message_updates_latest_message_without_changing_unread_count()
    {
        var sentAt = new DateTimeOffset(2026, 10, 12, 19, 0, 0, TimeSpan.Zero);
        var conversation = new Conversation(Guid.NewGuid(), 123, sentAt.AddMinutes(-2));
        conversation.RegisterInboundMessage("Inbound", sentAt.AddMinutes(-1));

        conversation.RegisterOutboundMessage(" Reply ", sentAt);

        Assert.Equal(1, conversation.UnreadCount);
        Assert.Equal("Reply", conversation.LastMessage);
        Assert.Equal(sentAt, conversation.LastMessageAt);
    }
}
