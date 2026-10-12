using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EncodeLabs.Inbox.Application.OutgoingMessages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EncodeLabs.Inbox.Infrastructure.Telegram;

internal sealed class TelegramMessageSender(
    HttpClient httpClient,
    IOptions<TelegramOptions> options,
    ILogger<TelegramMessageSender> logger) : ITelegramMessageSender
{
    public async Task<TelegramSendResult> SendTextMessageAsync(
        long chatId,
        string text,
        CancellationToken cancellationToken)
    {
        var botToken = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            logger.LogError("Telegram message sending failed because the bot token is not configured");
            return TelegramSendResult.Failed(TelegramSendFailure.NotConfigured);
        }

        try
        {
            var endpoint = new Uri(
                $"https://api.telegram.org/bot{botToken}/sendMessage",
                UriKind.Absolute);
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["chat_id"] = chatId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["text"] = text,
                });
            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Telegram rejected an outgoing message. ChatId: {ChatId}, StatusCode: {StatusCode}",
                    chatId,
                    (int)response.StatusCode);
                return TelegramSendResult.Failed(TelegramSendFailure.Rejected);
            }

            var result = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(
                cancellationToken);
            if (result is not { Ok: true, Result: { MessageId: > 0, Date: > 0 } message })
            {
                logger.LogWarning(
                    "Telegram returned an unsuccessful response for an outgoing message. ChatId: {ChatId}",
                    chatId);
                return TelegramSendResult.Failed(TelegramSendFailure.Rejected);
            }

            return TelegramSendResult.Success(
                message.MessageId,
                DateTimeOffset.FromUnixTimeSeconds(message.Date));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Telegram timed out sending an outgoing message. ChatId: {ChatId}", chatId);
            return TelegramSendResult.Failed(TelegramSendFailure.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Telegram was unavailable while sending an outgoing message. ChatId: {ChatId}",
                chatId);
            return TelegramSendResult.Failed(TelegramSendFailure.Unavailable);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(
                exception,
                "Telegram returned an invalid response while sending an outgoing message. ChatId: {ChatId}",
                chatId);
            return TelegramSendResult.Failed(TelegramSendFailure.Rejected);
        }
    }

    private sealed record TelegramApiResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] TelegramMessageResponse? Result);

    private sealed record TelegramMessageResponse(
        [property: JsonPropertyName("message_id")] long MessageId,
        [property: JsonPropertyName("date")] long Date);
}
