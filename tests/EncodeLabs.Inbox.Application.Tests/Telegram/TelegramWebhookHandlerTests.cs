using EncodeLabs.Inbox.Application.Telegram;
using System.Text.Json;

namespace EncodeLabs.Inbox.Application.Tests.Telegram;

public sealed class TelegramWebhookHandlerTests
{
    [Fact]
    public void Telegram_payload_is_deserialized_with_expected_field_names()
    {
        const string json = """
            {
              "update_id": 1001,
              "message": {
                "message_id": 2002,
                "from": {
                  "id": 3003,
                  "username": "ada",
                  "first_name": "Ada",
                  "last_name": "Lovelace"
                },
                "chat": { "id": 4004 },
                "date": 1700000000,
                "text": "Hello"
              }
            }
            """;

        var update = JsonSerializer.Deserialize<TelegramUpdate>(json);

        Assert.NotNull(update);
        Assert.Equal(1001, update.UpdateId);
        Assert.Equal(2002, update.Message?.MessageId);
        Assert.Equal(3003, update.Message?.From?.Id);
        Assert.Equal(4004, update.Message?.Chat?.Id);
        Assert.Equal("Hello", update.Message?.Text);
    }

    [Fact]
    public async Task Valid_text_message_is_mapped_and_published_once()
    {
        var publisher = new RecordingPublisher();
        var handler = new TelegramWebhookHandler(publisher);
        var update = new TelegramUpdate(
            1001,
            new TelegramMessage(
                2002,
                new TelegramUser(3003, "ada", "Ada", "Lovelace"),
                new TelegramChat(4004),
                1_700_000_000,
                "Hello"));

        var published = await handler.HandleAsync(update, CancellationToken.None);

        Assert.True(published);
        var message = Assert.Single(publisher.Messages);
        Assert.Equal(1001, message.UpdateId);
        Assert.Equal(2002, message.MessageId);
        Assert.Equal(4004, message.ChatId);
        Assert.Equal(3003, message.TelegramUserId);
        Assert.Equal("ada", message.Username);
        Assert.Equal("Ada", message.FirstName);
        Assert.Equal("Lovelace", message.LastName);
        Assert.Equal("Hello", message.Text);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), message.SentAt);
    }

    [Fact]
    public async Task Update_without_text_is_ignored()
    {
        var publisher = new RecordingPublisher();
        var handler = new TelegramWebhookHandler(publisher);
        var update = CreateUpdate(text: null);

        var published = await handler.HandleAsync(update, CancellationToken.None);

        Assert.False(published);
        Assert.Empty(publisher.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_text_is_ignored(string text)
    {
        var publisher = new RecordingPublisher();
        var handler = new TelegramWebhookHandler(publisher);
        var update = CreateUpdate(text);

        var published = await handler.HandleAsync(update, CancellationToken.None);

        Assert.False(published);
        Assert.Empty(publisher.Messages);
    }

    private static TelegramUpdate CreateUpdate(string? text) =>
        new(
            1,
            new TelegramMessage(
                2,
                new TelegramUser(3, null, "Test", null),
                new TelegramChat(4),
                1_700_000_000,
                text));

    private sealed class RecordingPublisher : IIncomingMessagePublisher
    {
        public List<IncomingTelegramMessage> Messages { get; } = [];

        public Task PublishAsync(
            IncomingTelegramMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
