namespace EncodeLabs.Inbox.Application.IncomingMessages;

public sealed class InvalidIncomingMessageException(string message) : Exception(message);
