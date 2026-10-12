using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EncodeLabs.Inbox.Infrastructure.Messaging;

internal sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqTopologyInitializer> logger) : BackgroundService
{
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);
                await RabbitMqTopology.DeclareAsync(channel, stoppingToken);

                logger.LogInformation(
                    "RabbitMQ incoming Telegram topology declared. Exchange: {Exchange}, Queue: {Queue}, DeadLetterQueue: {DeadLetterQueue}",
                    RabbitMqTopology.Exchange,
                    RabbitMqTopology.Queue,
                    RabbitMqTopology.DeadLetterQueue);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "RabbitMQ topology initialization attempt {Attempt}/{MaxAttempts} failed with {ErrorType}",
                    attempt,
                    MaxAttempts,
                    exception.GetType().Name);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        logger.LogError(
            "RabbitMQ topology could not be initialized after {MaxAttempts} attempts; the API will remain available",
            MaxAttempts);
    }
}
