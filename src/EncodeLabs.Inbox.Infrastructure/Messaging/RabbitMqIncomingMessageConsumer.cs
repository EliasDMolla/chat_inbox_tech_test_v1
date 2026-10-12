using System.Text.Json;
using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Application.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EncodeLabs.Inbox.Infrastructure.Messaging;

internal sealed class RabbitMqIncomingMessageConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqIncomingMessageConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: stoppingToken);
        await RabbitMqTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
            await HandleDeliveryAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            RabbitMqTopology.Queue,
            autoAck: false,
            consumer,
            stoppingToken);

        logger.LogInformation(
            "RabbitMQ consumer started for queue {Queue}",
            RabbitMqTopology.Queue);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        IncomingTelegramMessage? incoming = null;

        try
        {
            incoming = JsonSerializer.Deserialize<IncomingTelegramMessage>(delivery.Body.Span)
                ?? throw new JsonException("The RabbitMQ message body is null.");

            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessIncomingMessageHandler>();
            var result = await handler.HandleAsync(incoming, stoppingToken);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);

            if (result.Status == IncomingMessageProcessingStatus.Duplicate)
            {
                logger.LogInformation(
                    "Duplicate Telegram update ignored idempotently. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}",
                    incoming.UpdateId,
                    incoming.MessageId,
                    incoming.ChatId);
                return;
            }

            logger.LogInformation(
                "Incoming Telegram message persisted and acknowledged. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}, ConversationId: {ConversationId}, ContactId: {ContactId}",
                incoming.UpdateId,
                incoming.MessageId,
                incoming.ChatId,
                result.ConversationId,
                result.ContactId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Closing the channel returns the unacknowledged delivery to RabbitMQ.
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidIncomingMessageException)
        {
            await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, stoppingToken);
            logger.LogWarning(
                exception,
                "Invalid RabbitMQ message rejected to DLQ. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}",
                incoming?.UpdateId,
                incoming?.MessageId,
                incoming?.ChatId);
        }
        catch (Exception exception)
        {
            var requeue = !delivery.Redelivered;
            await channel.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue,
                stoppingToken);

            if (requeue)
            {
                logger.LogWarning(
                    exception,
                    "Incoming Telegram message processing failed transiently and will be retried once. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}",
                    incoming?.UpdateId,
                    incoming?.MessageId,
                    incoming?.ChatId);
            }
            else
            {
                logger.LogError(
                    exception,
                    "Incoming Telegram message processing failed after retry and was sent to DLQ. TelegramUpdateId: {TelegramUpdateId}, TelegramMessageId: {TelegramMessageId}, TelegramChatId: {TelegramChatId}",
                    incoming?.UpdateId,
                    incoming?.MessageId,
                    incoming?.ChatId);
            }
        }
    }
}
