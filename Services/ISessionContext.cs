using Obcred.Models;

namespace Obcred.Services;

/// <summary>
/// Holds the current signed-in session in memory so other services (usage
/// reporting, and later plan/billing calls) can grab the session token
/// without re-reading it from disk each time. Set once, right after login
/// succeeds — whether that's a fresh Google sign-in or a silently restored
/// cached session.
/// </summary>
public interface ISessionContext
{
    GoogleAuthResult? Current { get; }
    void SetCurrent(GoogleAuthResult session);
}