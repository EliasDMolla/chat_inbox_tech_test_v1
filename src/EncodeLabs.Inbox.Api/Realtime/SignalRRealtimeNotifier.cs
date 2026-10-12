using EncodeLabs.Inbox.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace EncodeLabs.Inbox.Api.Realtime;

public sealed class SignalRRealtimeNotifier(
    IHubContext<ConversationsHub> hubContext,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task NotifyMessageReceivedAsync(
        MessageReceivedRealtimeDto message,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                "MessageReceived",
                message,
                cancellationToken);
            logger.LogInformation(
                "SignalR MessageReceived emitted. ConversationId: {ConversationId}, MessageId: {MessageId}",
                message.ConversationId,
                message.Message.Id);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error emitting SignalR MessageReceived. ConversationId: {ConversationId}, MessageId: {MessageId}",
                message.ConversationId,
                message.Message.Id);
            throw;
        }
    }

    public async Task NotifyConversationUpdatedAsync(
        ConversationUpdatedRealtimeDto conversation,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                "ConversationUpdated",
                conversation,
                cancellationToken);
            logger.LogInformation(
                "SignalR ConversationUpdated emitted. ConversationId: {ConversationId}, ContactId: {ContactId}",
                conversation.ConversationId,
                conversation.ContactId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error emitting SignalR ConversationUpdated. ConversationId: {ConversationId}, ContactId: {ContactId}",
                conversation.ConversationId,
                conversation.ContactId);
            throw;
        }
    }

    public async Task NotifyMessagesReadAsync(
        MessagesReadRealtimeDto messagesRead,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                "MessagesRead",
                messagesRead,
                cancellationToken);
            logger.LogInformation(
                "SignalR MessagesRead emitted. ConversationId: {ConversationId}, MessageCount: {MessageCount}",
                messagesRead.ConversationId,
                messagesRead.MessageIds.Count);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Error emitting SignalR MessagesRead. ConversationId: {ConversationId}, MessageCount: {MessageCount}",
                messagesRead.ConversationId,
                messagesRead.MessageIds.Count);
            throw;
        }
    }
}
