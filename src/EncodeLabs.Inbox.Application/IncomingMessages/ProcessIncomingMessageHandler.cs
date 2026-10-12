using EncodeLabs.Inbox.Application.Telegram;

namespace EncodeLabs.Inbox.Application.IncomingMessages;

public sealed class ProcessIncomingMessageHandler(IIncomingMessageStore store)
{
    public Task<IncomingMessageProcessingResult> HandleAsync(
        IncomingTelegramMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.UpdateId <= 0 ||
            message.MessageId <= 0 ||
            message.ChatId == 0 ||
            message.TelegramUserId <= 0 ||
            string.IsNullOrWhiteSpace(message.Text))
        {
            throw new InvalidIncomingMessageException(
                "The incoming Telegram message does not contain all required fields.");
        }

        if (message.SentAt == default)
        {
            throw new InvalidIncomingMessageException(
                "The incoming Telegram message has an invalid sent date.");
        }

        return store.StoreAsync(message, cancellationToken);
    }
}
