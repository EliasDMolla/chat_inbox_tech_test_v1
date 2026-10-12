using System.ComponentModel.DataAnnotations;
using EncodeLabs.Inbox.Application.Conversations;
using EncodeLabs.Inbox.Application.OutgoingMessages;
using Microsoft.AspNetCore.Mvc;

namespace EncodeLabs.Inbox.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(
    GetConversationsHandler getConversations,
    GetConversationMessagesHandler getMessages,
    MarkConversationAsReadHandler markAsRead,
    SendOutgoingMessageHandler sendMessage) : ControllerBase
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

    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<OutgoingMessageDto>> SendMessageAsync(
        Guid conversationId,
        SendConversationMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sendMessage.HandleAsync(
            conversationId,
            request.Text,
            cancellationToken);

        return result.Status switch
        {
            SendOutgoingMessageStatus.Sent => Ok(result.Message),
            SendOutgoingMessageStatus.NotFound => NotFound(),
            SendOutgoingMessageStatus.InvalidText => BadRequest(new ProblemDetails
            {
                Title = "Invalid message text",
                Detail = "Text is required, cannot contain only whitespace, and cannot exceed 4096 characters.",
                Status = StatusCodes.Status400BadRequest,
            }),
            SendOutgoingMessageStatus.TelegramFailed => StatusCode(
                result.TelegramFailure == TelegramSendFailure.Rejected
                    ? StatusCodes.Status502BadGateway
                    : StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails
                {
                    Title = "Telegram message delivery failed",
                    Detail = "The message could not be delivered to Telegram.",
                    Status = result.TelegramFailure == TelegramSendFailure.Rejected
                        ? StatusCodes.Status502BadGateway
                        : StatusCodes.Status503ServiceUnavailable,
                }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}

public sealed class SendConversationMessageRequest
{
    [Required]
    [MaxLength(SendOutgoingMessageHandler.MaximumTextLength)]
    public string? Text { get; init; }
}
