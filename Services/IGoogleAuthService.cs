using System.Threading;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public interface IGoogleAuthService
{
    /// <summary>True while a login is in progress (browser open, waiting on the redirect).</summary>
    bool IsLoggingIn { get; }

    /// <summary>
    /// Tries to restore a previously saved session without opening a browser.
    /// Returns null if there is no saved session, or it has expired.
    /// </summary>
    Task<GoogleAuthResult?> TryRestoreSessionAsync();

    /// <summary>
    /// Opens the system browser to the Worker's /auth/google route and waits for the
    /// redirect back to a local loopback listener. Throws on cancellation, timeout,
    /// or a failed/tampered response.
    /// </summary>
    Task<GoogleAuthResult> LoginWithGoogleAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears any saved local session. Does not revoke access on Google's side.</summary>
    void Logout();
}