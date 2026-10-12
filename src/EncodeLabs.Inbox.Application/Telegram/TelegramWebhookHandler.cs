namespace EncodeLabs.Inbox.Application.Telegram;

public sealed class TelegramWebhookHandler(IIncomingMessagePublisher publisher)
{
    public async Task<bool> HandleAsync(
        TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        var message = update.Message;

        if (message?.Chat is null ||
            message.From is null ||
            string.IsNullOrWhiteSpace(message.Text))
        {
            return false;
        }

        DateTimeOffset sentAt;

        try
        {
            sentAt = DateTimeOffset.FromUnixTimeSeconds(message.SentAtUnixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var incomingMessage = new IncomingTelegramMessage(
            update.UpdateId,
            message.MessageId,
            message.Chat.Id,
            message.From.Id,
            message.From.Username,
            message.From.FirstName,
            message.From.LastName,
            message.Text,
            sentAt);

        await publisher.PublishAsync(incomingMessage, cancellationToken);
        return true;
    }
}
