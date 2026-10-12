using EncodeLabs.Inbox.Application.Telegram;
using Microsoft.AspNetCore.Mvc;

namespace EncodeLabs.Inbox.Api.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
public sealed class TelegramWebhookController(
    TelegramWebhookHandler handler,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveAsync(
        [FromBody] TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Telegram update received. TelegramUpdateId: {TelegramUpdateId}",
            update.UpdateId);

        var published = await handler.HandleAsync(update, cancellationToken);

        if (!published)
        {
            logger.LogInformation(
                "Telegram update ignored because its type or content is unsupported. TelegramUpdateId: {TelegramUpdateId}",
                update.UpdateId);
        }

        return Ok();
    }
}
