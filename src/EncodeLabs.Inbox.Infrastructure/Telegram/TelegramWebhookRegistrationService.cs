using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EncodeLabs.Inbox.Infrastructure.Telegram;

internal sealed class TelegramWebhookRegistrationService(
    IOptions<TelegramOptions> telegramOptions,
    IOptions<NgrokOptions> ngrokOptions,
    ILogger<TelegramWebhookRegistrationService> logger) : BackgroundService
{
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botToken = telegramOptions.Value.BotToken;

        if (string.IsNullOrWhiteSpace(botToken))
        {
            logger.LogWarning(
                "Telegram webhook registration skipped because Telegram:BotToken is not configured");
            return;
        }

        var options = ngrokOptions.Value;

        for (var attempt = 1; attempt <= options.RegistrationAttempts; attempt++)
        {
            try
            {
                var publicUrl = await GetPublicHttpsUrlAsync(options.ApiUrl, stoppingToken);

                if (publicUrl is not null)
                {
                    var webhookUrl = new Uri(new Uri(publicUrl.TrimEnd('/') + "/"), "api/telegram/webhook");

                    if (await RegisterWebhookAsync(botToken, webhookUrl, stoppingToken))
                    {
                        logger.LogInformation(
                            "Telegram webhook registered successfully at {WebhookUrl}",
                            webhookUrl);
                        return;
                    }
                }

                logger.LogWarning(
                    "Telegram webhook registration attempt {Attempt}/{MaxAttempts} did not succeed",
                    attempt,
                    options.RegistrationAttempts);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Telegram webhook registration attempt {Attempt}/{MaxAttempts} failed with {ErrorType}",
                    attempt,
                    options.RegistrationAttempts,
                    exception.GetType().Name);
            }

            if (attempt < options.RegistrationAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.RetryDelaySeconds), stoppingToken);
            }
        }

        logger.LogError(
            "Telegram webhook could not be registered after {MaxAttempts} attempts; the API will remain available",
            options.RegistrationAttempts);
    }

    private async Task<string?> GetPublicHttpsUrlAsync(
        string apiUrl,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<NgrokTunnelsResponse>(
            apiUrl,
            cancellationToken);

        return response?.Tunnels
            .Select(tunnel => tunnel.PublicUrl)
            .FirstOrDefault(url =>
                Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task<bool> RegisterWebhookAsync(
        string botToken,
        Uri webhookUrl,
        CancellationToken cancellationToken)
    {
        var endpoint = new Uri(
            $"https://api.telegram.org/bot{botToken}/setWebhook",
            UriKind.Absolute);
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["url"] = webhookUrl.AbsoluteUri,
            });
        using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(
            cancellationToken);
        return result?.Ok == true;
    }

    public override void Dispose()
    {
        httpClient.Dispose();
        base.Dispose();
    }

    private sealed record NgrokTunnelsResponse(
        [property: JsonPropertyName("tunnels")] IReadOnlyCollection<NgrokTunnel> Tunnels);

    private sealed record NgrokTunnel(
        [property: JsonPropertyName("public_url")] string PublicUrl);

    private sealed record TelegramApiResponse(
        [property: JsonPropertyName("ok")] bool Ok);
}
