using EncodeLabs.Inbox.Application.Telegram;
using EncodeLabs.Inbox.Application.IncomingMessages;
using EncodeLabs.Inbox.Infrastructure.Messaging;
using EncodeLabs.Inbox.Infrastructure.Persistence;
using EncodeLabs.Inbox.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EncodeLabs.Inbox.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.Configure<RabbitMqOptions>(
            configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<TelegramOptions>(
            configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<NgrokOptions>(
            configuration.GetSection(NgrokOptions.SectionName));

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IIncomingMessagePublisher, RabbitMqIncomingMessagePublisher>();
        services.AddScoped<IIncomingMessageStore, EfIncomingMessageStore>();
        services.AddHostedService<DatabaseMigrationService>();
        services.AddHostedService<RabbitMqTopologyInitializer>();
        services.AddHostedService<RabbitMqIncomingMessageConsumer>();
        services.AddHostedService<TelegramWebhookRegistrationService>();

        return services;
    }
}
