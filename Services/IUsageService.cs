using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public interface IUsageService
{
    /// <summary>
    /// Reports one successful UJP submission for the current month. Best-effort:
    /// a network failure here is swallowed rather than thrown, so a flaky
    /// connection never blocks or fails an invoice that already went through.
    /// </summary>
    Task IncrementAsync();

    /// <summary>
    /// Current month's plan/usage snapshot, or null if it couldn't be fetched
    /// (offline, not logged in, Worker unreachable, etc.) — callers should
    /// just leave the last-known badge state in place in that case.
    /// </summary>
    Task<UsageStatus?> GetStatusAsync();
}