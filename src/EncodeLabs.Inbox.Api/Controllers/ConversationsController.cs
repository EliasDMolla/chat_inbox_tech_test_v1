using EncodeLabs.Inbox.Application.Conversations;
using Microsoft.AspNetCore.Mvc;

namespace EncodeLabs.Inbox.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    GetConversationsHandler getConversations,
    GetConversationMessagesHandler getMessages,
    MarkConversationAsReadHandler markAsRead) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> GetAsync(
        CancellationToken cancellationToken) =>
        Ok(await getConversations.HandleAsync(cancellationToken));

    [HttpGet("{conversationId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ConversationMessageDto>>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var messages = await getMessages.HandleAsync(conversationId, cancellationToken);
        return messages is null ? NotFound() : Ok(messages);
    }

    [HttpPost("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsReadAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var result = await markAsRead.HandleAsync(conversationId, cancellationToken);
        return result.Found ? NoContent() : NotFound();
    }
}
