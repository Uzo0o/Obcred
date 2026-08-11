using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Obcred.Models;

/// <summary>
/// One entry from POST /api/v1/documents/purchase-invoice/invoices-status — a status
/// snapshot of an invoice someone else has sent TO us (an incoming/"влезна" invoice).
/// UJP does not track payment status at all; that's local-only (see PurchaseInvoiceRecord).
/// </summary>
public class PurchaseInvoiceStatusDto
{
    [JsonPropertyName("euid")] public string Euid { get; set; } = string.Empty;
    [JsonPropertyName("statusCode")] public string StatusCode { get; set; } = string.Empty;
    [JsonPropertyName("statusName")] public string StatusName { get; set; } = string.Empty;
    [JsonPropertyName("docNumber")] public string DocNumber { get; set; } = string.Empty;
    [JsonPropertyName("docDate")] public string DocDate { get; set; } = string.Empty;
    [JsonPropertyName("docDeliveryDate")] public string? DocDeliveryDate { get; set; }
    [JsonPropertyName("docNetAmount")] public decimal DocNetAmount { get; set; }
    [JsonPropertyName("docVatAmount")] public decimal DocVatAmount { get; set; }
    [JsonPropertyName("docGrossAmountR")] public decimal DocGrossAmountR { get; set; }
    [JsonPropertyName("docAvansAmount")] public decimal DocAvansAmount { get; set; }
    [JsonPropertyName("docFinalAmount")] public decimal DocFinalAmount { get; set; }
    [JsonPropertyName("sellerTin")] public string SellerTin { get; set; } = string.Empty;
    [JsonPropertyName("buyerTin")] public string BuyerTin { get; set; } = string.Empty;
    [JsonPropertyName("senderTin")] public string? SenderTin { get; set; }
    [JsonPropertyName("receiverTin")] public string? ReceiverTin { get; set; }
}

internal class PurchaseInvoiceStatusResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("createdDate")] public string? CreatedDate { get; set; }
    [JsonPropertyName("errorStatus")] public string? ErrorStatus { get; set; }
    [JsonPropertyName("invoices")] public List<PurchaseInvoiceStatusDto> Invoices { get; set; } = new();
}