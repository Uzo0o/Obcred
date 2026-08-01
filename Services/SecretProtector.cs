using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Obcred.Services;

/// <summary>
/// Encrypts small secrets (the .pfx password) at rest using Windows DPAPI,
/// bound to the current Windows user account. Values are stored as
/// "DPAPI:" + Base64 so a legacy plaintext value can be detected and migrated.
///
/// Note: because encryption is tied to the Windows user, an encrypted value only
/// decrypts for the same user on the same machine — which is exactly what we want
/// for a locally-configured signing credential.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SecretProtector
{
    private const string Prefix = "DPAPI:";

    // Extra entropy, so the ciphertext is specific to this application.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Obcred.eFaktura.v1");

    public static bool IsProtected(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Prefix + Convert.ToBase64String(encrypted);
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
            return string.Empty;

        // Not our marker => a legacy plaintext value written before encryption existed.
        if (!IsProtected(stored))
            return stored;

        try
        {
            byte[] encrypted = Convert.FromBase64String(stored.Substring(Prefix.Length));
            byte[] data = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            // Corrupted, or encrypted by a different user/machine — treat as no password.
            return string.Empty;
        }
    }
}
