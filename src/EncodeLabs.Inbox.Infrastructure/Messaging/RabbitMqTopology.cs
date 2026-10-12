using RabbitMQ.Client;

namespace EncodeLabs.Inbox.Infrastructure.Messaging;

internal static class RabbitMqTopology
{
    public const string Exchange = "telegram.incoming";
    public const string Queue = "telegram.incoming.messages";
    public const string RoutingKey = "telegram.message.text";
    public const string DeadLetterExchange = "telegram.incoming.dlx";
    public const string DeadLetterQueue = "telegram.incoming.messages.dlq";
    public const string DeadLetterRoutingKey = "telegram.message.text.dead-letter";

    public static async Task DeclareAsync(
        IChannel channel,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            DeadLetterQueue,
            DeadLetterExchange,
            DeadLetterRoutingKey,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey,
        };

        await channel.QueueDeclareAsync(
            Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            Queue,
            Exchange,
            RoutingKey,
            cancellationToken: cancellationToken);
    }
}
