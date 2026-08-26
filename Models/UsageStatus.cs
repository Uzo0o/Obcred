using System.Text.Json.Serialization;

namespace Obcred.Models;

/// <summary>The current month's plan/usage snapshot, as returned by GET /usage/status.</summary>
public class UsageStatus
{
    [JsonPropertyName("yearMonth")] public string YearMonth { get; set; } = string.Empty;
    [JsonPropertyName("used")] public int Used { get; set; }

    // null means unlimited.
    [JsonPropertyName("limit")] public int? Limit { get; set; }
    [JsonPropertyName("remaining")] public int? Remaining { get; set; }

    [JsonPropertyName("plan")] public string Plan { get; set; } = "free";
}