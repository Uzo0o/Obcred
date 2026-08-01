using System.Collections.Generic;
using System.Linq;

namespace Obcred.Models;

/// <summary>
/// A single VAT rate: how it appears in the UI, the UJP API code, and the percent.
/// This is the ONE place tax rates are defined — the dropdown, the per-item payload,
/// and the VAT totals block all derive from here.
/// </summary>
public sealed class VatRate
{
    public required string DisplayName { get; init; }  // shown in the dropdown, e.g. "18%"
    public required string ApiCode { get; init; }      // UJP indicator, e.g. "DDV-A"
    public required decimal Percent { get; init; }     // 18.0, 10.0, 5.0, 0.0

    public decimal Fraction => Percent / 100m;
}

public static class VatRates
{
    public static readonly VatRate Standard = new() { DisplayName = "18%", ApiCode = "DDV-A", Percent = 18.0m };

    // North Macedonia's 10% rate.
    // TODO: verify "DDV-V" is the correct UJP indicator for 10% against the UJP wiki before going live.
    public static readonly VatRate Reduced10 = new() { DisplayName = "10%", ApiCode = "DDV-V", Percent = 10.0m };

    public static readonly VatRate Reduced5 = new() { DisplayName = "5%", ApiCode = "DDV-B", Percent = 5.0m };
    public static readonly VatRate Exempt = new() { DisplayName = "0% (Exempt)", ApiCode = "DDV-G", Percent = 0.0m };

    public static readonly IReadOnlyList<VatRate> All = new[] { Standard, Reduced10, Reduced5, Exempt };

    /// <summary>Display strings for binding to the dropdown.</summary>
    public static readonly string[] DisplayNames = All.Select(r => r.DisplayName).ToArray();

    /// <summary>Resolve a rate from its dropdown text; unknown values fall back to Exempt (0%).</summary>
    public static VatRate FromDisplayName(string? displayName) =>
        All.FirstOrDefault(r => r.DisplayName == displayName) ?? Exempt;
}
