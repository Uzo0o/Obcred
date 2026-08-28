using System.Collections.Generic;
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

    /// <summary>All plan tiers (id, limit, price), sorted by rank — for the plan picker.</summary>
    Task<List<PlanInfo>?> GetPlansAsync();

    /// <summary>
    /// Selects or upgrades this month's plan. The first selection in a given
    /// month is unrestricted; later selections in the SAME month must be an
    /// upgrade — a downgrade attempt comes back with DowngradeRejected=true
    /// rather than an exception, so the UI can show a clear message.
    /// </summary>
    Task<PlanSelectResult> SelectPlanAsync(string planId);

    /// <summary>
    /// True if the person has already dismissed this month's "you're over the
    /// Free plan, you'll be billed per invoice" prompt — tracked locally so we
    /// don't re-show it on every single submission once acknowledged.
    /// </summary>
    bool HasAcknowledgedOverageThisMonth();

    void AcknowledgeOverageThisMonth();

    /// <summary>
    /// Sends a UJP-verified EDB to the Worker so it's on file for the future
    /// self-invoicer. Call ONLY after UJP itself has confirmed the EDB is
    /// valid (i.e. right after a successful company lookup) — never a raw,
    /// unverified value the person just typed. Best-effort: returns false on
    /// any failure rather than throwing, since this should never block Settings
    /// from completing.
    /// </summary>
    Task<bool> SyncEdbAsync(string edb);
}