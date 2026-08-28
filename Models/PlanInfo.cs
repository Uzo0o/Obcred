using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Obcred.Models;

public class PlanInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("rank")] public int Rank { get; set; }

    // null = unlimited (business tier).
    [JsonPropertyName("limit")] public int? Limit { get; set; }
    [JsonPropertyName("price")] public int Price { get; set; }

    public string DisplayName => char.ToUpper(Id[0]) + Id[1..];
    public string LimitLabel => Limit is null ? "Unlimited invoices" : $"{Limit} invoices/month";
    public string PriceLabel => Price == 0 ? "Free" : $"{Price} MKD/month";
}

internal class PlansResponse
{
    [JsonPropertyName("plans")] public List<PlanInfo> Plans { get; set; } = new();
    [JsonPropertyName("overagePerInvoice")] public int OveragePerInvoice { get; set; }
}