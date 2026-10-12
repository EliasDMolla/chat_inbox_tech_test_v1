namespace EncodeLabs.Inbox.Infrastructure.Telegram;

public sealed class NgrokOptions
{
    public const string SectionName = "Ngrok";

    public string ApiUrl { get; init; } = "http://localhost:4040/api/tunnels";

    public int RegistrationAttempts { get; init; } = 10;

    public int RetryDelaySeconds { get; init; } = 3;
}
