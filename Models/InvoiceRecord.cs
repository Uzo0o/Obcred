using System;
using SQLite;

namespace Obcred.Models;

/// <summary>
/// A permanent local record of every invoice submission attempt.
/// This is the legal/audit trail: it stores the exact JSON we built, the exact
/// signed JWS we sent, and the raw response UJP gave back — so a submission can
/// always be reproduced, re-printed, or investigated later.
/// </summary>
public class InvoiceRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string DocNumber { get; set; } = string.Empty;   // e.g. INV-2026-0001
    public string DocType { get; set; } = string.Empty;     // 100 / 380 / 383
    public string DocId { get; set; } = string.Empty;       // the docId we generated for UJP

    public string BuyerEdb { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;

    public string IssueDate { get; set; } = string.Empty;   // yyyy-MM-dd
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "MKD";

    /// <summary>Draft, Sent, or Failed.</summary>
    public string Status { get; set; } = "Draft";
    public int HttpStatusCode { get; set; }

    public string PayloadJson { get; set; } = string.Empty;  // exact JSON document we built
    public string SignedJws { get; set; } = string.Empty;    // exact signed document we transmitted
    public string UjpResponse { get; set; } = string.Empty;  // raw UJP reply or error text
    public string PdfModelJson { get; set; } = string.Empty; // snapshot for re-printing a PDF later
}
