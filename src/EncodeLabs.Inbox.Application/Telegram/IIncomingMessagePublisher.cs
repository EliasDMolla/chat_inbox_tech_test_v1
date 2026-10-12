namespace EncodeLabs.Inbox.Application.Telegram;

public interface IIncomingMessagePublisher
{
    Task PublishAsync(
        IncomingTelegramMessage message,
        CancellationToken cancellationToken);
}
