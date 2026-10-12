namespace EncodeLabs.Inbox.Domain.Messages;

public sealed class Message
{
    private Message()
    {
    }

    private Message(
        Guid conversationId,
        long telegramMessageId,
        long? telegramUpdateId,
        MessageDirection direction,
        string text,
        bool isRead,
        DateTimeOffset sentAt,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Message text cannot be empty.", nameof(text));
        }

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        TelegramMessageId = telegramMessageId;
        TelegramUpdateId = telegramUpdateId;
        Direction = direction;
        Text = text.Trim();
        IsRead = isRead;
        SentAt = sentAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public long TelegramMessageId { get; private set; }

    public long? TelegramUpdateId { get; private set; }

    public MessageDirection Direction { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public bool IsRead { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Message CreateInbound(
        Guid conversationId,
        long telegramMessageId,
        long telegramUpdateId,
        string text,
        DateTimeOffset sentAt,
        DateTimeOffset createdAt) =>
        new(
            conversationId,
            telegramMessageId,
            telegramUpdateId,
            MessageDirection.Inbound,
            text,
            false,
            sentAt,
            createdAt);

    public static Message CreateOutbound(
        Guid conversationId,
        long telegramMessageId,
        string text,
        DateTimeOffset sentAt,
        DateTimeOffset createdAt) =>
        new(
            conversationId,
            telegramMessageId,
            null,
            MessageDirection.Outbound,
            text,
            true,
            sentAt,
            createdAt);

    public bool MarkAsRead()
    {
        if (Direction != MessageDirection.Inbound || IsRead)
        {
            return false;
        }

        IsRead = true;
        return true;
    }
}
