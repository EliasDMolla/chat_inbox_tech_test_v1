using EncodeLabs.Inbox.Application.Telegram;

namespace EncodeLabs.Inbox.Application.IncomingMessages;

public interface IIncomingMessageStore
{
    Task<IncomingMessageProcessingResult> StoreAsync(
        IncomingTelegramMessage message,
        CancellationToken cancellationToken);
}
