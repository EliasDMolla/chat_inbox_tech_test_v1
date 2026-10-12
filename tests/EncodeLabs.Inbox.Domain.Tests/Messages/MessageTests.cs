using EncodeLabs.Inbox.Domain.Messages;

namespace EncodeLabs.Inbox.Domain.Tests.Messages;

public sealed class MessageTests
{
    [Fact]
    public void Inbound_message_is_created_unread()
    {
        var sentAt = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        var message = Message.CreateInbound(
            Guid.NewGuid(),
            20,
            10,
            " Hello ",
            sentAt,
            sentAt.AddSeconds(1));

        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.False(message.IsRead);
        Assert.Equal("Hello", message.Text);
        Assert.Equal(10, message.TelegramUpdateId);
        Assert.Equal(20, message.TelegramMessageId);
    }

    [Fact]
    public void Empty_inbound_message_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Message.CreateInbound(
            Guid.NewGuid(),
            20,
            10,
            " ",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Inbound_message_is_marked_as_read_idempotently()
    {
        var message = Message.CreateInbound(
            Guid.NewGuid(),
            20,
            10,
            "Hello",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var firstChange = message.MarkAsRead();
        var secondChange = message.MarkAsRead();

        Assert.True(firstChange);
        Assert.False(secondChange);
        Assert.True(message.IsRead);
    }
}
