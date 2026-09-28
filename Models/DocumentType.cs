using System.Collections.Generic;
using System.Linq;

namespace Obcred.Models;

/// <summary>
/// A submittable UJP document type: the code sent as docType and the exact name UJP
/// expects back as docTypeName. Confirmed against the live sandbox's
/// GET /api/v1/document-types (2026-09-22) — UJP defines ~25 document types in total;
/// only the ones this app actually supports submitting are listed here.
/// </summary>
public sealed class DocumentType
{
    public required string Code { get; init; }   // UJP docType, e.g. "100"
    public required string Name { get; init; }   // UJP docTypeName, e.g. "Фактура" — shown in the dropdown AND sent as-is

    public override string ToString() => Name;
}

public static class DocumentTypes
{
    public static readonly DocumentType Invoice = new() { Code = "100", Name = "Фактура" };
    public static readonly DocumentType AdvanceInvoice = new() { Code = "130", Name = "Авансна фактура" };

    public static readonly IReadOnlyList<DocumentType> All = new[] { Invoice, AdvanceInvoice };

    /// <summary>Resolve a type from its docType code; unknown/missing codes fall back to a regular Invoice.</summary>
    public static DocumentType FromCode(string? code) =>
        All.FirstOrDefault(t => t.Code == code) ?? Invoice;
}
