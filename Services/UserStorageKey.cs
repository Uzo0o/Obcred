using System.IO;
using System.Linq;

namespace Obcred.Services;

/// <summary>
/// Turns a signed-in account's id into a filesystem-safe folder name, so each
/// account's local data (invoices, clients, settings) lives under its own
/// subfolder instead of one shared file for everyone who logs into the app.
/// </summary>
internal static class UserStorageKey
{
    public static string From(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "unknown-user";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] safe = userId.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(safe);
    }
}
