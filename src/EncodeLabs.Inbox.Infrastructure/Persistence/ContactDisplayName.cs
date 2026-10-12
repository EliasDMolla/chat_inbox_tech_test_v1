using System.Globalization;
using EncodeLabs.Inbox.Domain.Contacts;

namespace EncodeLabs.Inbox.Infrastructure.Persistence;

internal static class ContactDisplayName
{
    public static string Build(Contact contact) =>
        Build(
            contact.FirstName,
            contact.LastName,
            contact.Username,
            contact.TelegramUserId);

    public static string Build(
        string? firstName,
        string? lastName,
        string? username,
        long telegramUserId)
    {
        var fullName = string.Join(
            ' ',
            new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return string.IsNullOrEmpty(fullName)
            ? username ?? telegramUserId.ToString(CultureInfo.InvariantCulture)
            : fullName;
    }
}
