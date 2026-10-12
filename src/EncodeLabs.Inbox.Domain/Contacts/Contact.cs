namespace EncodeLabs.Inbox.Domain.Contacts;

public sealed class Contact
{
    private Contact()
    {
    }

    public Contact(
        long telegramUserId,
        string? username,
        string? firstName,
        string? lastName,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        TelegramUserId = telegramUserId;
        Username = NormalizeOptional(username);
        FirstName = NormalizeOptional(firstName);
        LastName = NormalizeOptional(lastName);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public long TelegramUserId { get; private set; }

    public string? Username { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateProfile(string? username, string? firstName, string? lastName)
    {
        Username = NormalizeOptional(username);
        FirstName = NormalizeOptional(firstName);
        LastName = NormalizeOptional(lastName);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
