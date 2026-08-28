namespace Obcred.Models;

/// <summary>Outcome of a POST /plan/select call.</summary>
public class PlanSelectResult
{
    public bool Success { get; set; }
    public string? Plan { get; set; }
    public int? Limit { get; set; }
    public int? Price { get; set; }

    /// <summary>True specifically when the Worker rejected this as a downgrade (HTTP 409).</summary>
    public bool DowngradeRejected { get; set; }

    public string? ErrorMessage { get; set; }
}