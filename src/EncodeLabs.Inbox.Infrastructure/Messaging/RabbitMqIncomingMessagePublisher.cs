using System.Text.Json;
using EncodeLabs.Inbox.Application.Telegram;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EncodeLabs.Inbox.Infrastructure.Messaging;

internal sealed class RabbitMqIncomingMessagePublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqIncomingMessagePublisher> logger) : IIncomingMessagePublisher
{
    public async Task PublishAsync(
        IncomingTelegramMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

            await RabbitMqTopology.DeclareAsync(channel, cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = message.UpdateId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            await channel.BasicPublishAsync(
                RabbitMqTopology.Exchange,
                RabbitMqTopology.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "Telegram message published to RabbitMQ. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}, TelegramUserId: {TelegramUserId}",
                message.UpdateId,
                message.MessageId,
                message.ChatId,
                message.TelegramUserId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error publishing Telegram message to RabbitMQ. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}",
                message.UpdateId,
                message.MessageId);
            throw;
        }
    }

}
