using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Application.Telegram;

namespace EncodeLabs.Inbox.Application.Tests.IncomingMessages;

public sealed class ProcessIncomingMessageHandlerTests
{
    [Fact]
    public async Task Valid_message_is_delegated_to_store()
    {
        var store = new RecordingStore(new(IncomingMessageProcessingStatus.Processed));
        var handler = new ProcessIncomingMessageHandler(store);
        var message = CreateMessage();

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(IncomingMessageProcessingStatus.Processed, result.Status);
        Assert.Same(message, Assert.Single(store.Messages));
    }

    [Fact]
    public async Task Duplicate_result_is_returned_as_successful_outcome()
    {
        var store = new RecordingStore(new(IncomingMessageProcessingStatus.Duplicate));
        var handler = new ProcessIncomingMessageHandler(store);

        var result = await handler.HandleAsync(CreateMessage(), CancellationToken.None);

        Assert.Equal(IncomingMessageProcessingStatus.Duplicate, result.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Invalid_text_is_rejected_before_persistence(string text)
    {
        var store = new RecordingStore(new(IncomingMessageProcessingStatus.Processed));
        var handler = new ProcessIncomingMessageHandler(store);

        await Assert.ThrowsAsync<InvalidIncomingMessageException>(() =>
            handler.HandleAsync(CreateMessage(text), CancellationToken.None));
        Assert.Empty(store.Messages);
    }

    private static IncomingTelegramMessage CreateMessage(string text = "Hello") =>
        new(1, 2, 3, 4, "ada", "Ada", "Lovelace", text, DateTimeOffset.UtcNow);

    private sealed class RecordingStore(IncomingMessageProcessingResult result)
        : IIncomingMessageStore
    {
        public List<IncomingTelegramMessage> Messages { get; } = [];

        public Task<IncomingMessageProcessingResult> StoreAsync(
            IncomingTelegramMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(result);
        }
    }
}
