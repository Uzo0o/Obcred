using Obcred.Models;

namespace Obcred.Services;

public class SessionContext : ISessionContext
{
    public GoogleAuthResult? Current { get; private set; }

    public void SetCurrent(GoogleAuthResult session) => Current = session;
}