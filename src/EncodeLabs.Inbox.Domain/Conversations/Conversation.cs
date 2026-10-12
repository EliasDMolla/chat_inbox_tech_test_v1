namespace EncodeLabs.Inbox.Domain.Conversations;

public sealed class Conversation
{
    private Conversation()
    {
    }

    public Conversation(Guid contactId, long telegramChatId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ContactId = contactId;
        TelegramChatId = telegramChatId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ContactId { get; private set; }

    public long TelegramChatId { get; private set; }

    public int UnreadCount { get; private set; }

    public string? LastMessage { get; private set; }

    public DateTimeOffset? LastMessageAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void RegisterInboundMessage(string text, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Message text cannot be empty.", nameof(text));
        }

        checked
        {
            UnreadCount++;
        }

        if (LastMessageAt is null || sentAt >= LastMessageAt)
        {
            LastMessage = text.Trim();
            LastMessageAt = sentAt;
        }
    }

    public bool MarkAsRead()
    {
        if (UnreadCount == 0)
        {
            return false;
        }

        UnreadCount = 0;
        return true;
    }
}
