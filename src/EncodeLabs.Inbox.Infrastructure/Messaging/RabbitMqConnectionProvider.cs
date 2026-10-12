using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EncodeLabs.Inbox.Infrastructure.Messaging;

internal sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection? connection;

    public async Task<IConnection> GetConnectionAsync(
        CancellationToken cancellationToken)
    {
        if (connection is { IsOpen: true })
        {
            return connection;
        }

        await connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (connection is { IsOpen: true })
            {
                return connection;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            var rabbitMqOptions = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = rabbitMqOptions.Host,
                Port = rabbitMqOptions.Port,
                UserName = rabbitMqOptions.Username,
                Password = rabbitMqOptions.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        connectionLock.Dispose();
    }
}
