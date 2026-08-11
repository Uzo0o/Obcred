using System.Collections.Generic;

namespace Obcred.Models;

public class PdfTemplateOption
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public static readonly IReadOnlyList<PdfTemplateOption> All = new List<PdfTemplateOption>
    {
        new()
        {
            Id = "Classic",
            DisplayName = "Classic",
            Description = "Bordered seller/buyer boxes, a clean ruled table. Traditional and safe."
        },
        new()
        {
            Id = "Modern",
            DisplayName = "Modern",
            Description = "Bold accent header band with your logo, shaded table rows, a stronger totals block."
        },
        new()
        {
            Id = "Minimal",
            DisplayName = "Minimal",
            Description = "No borders or shading — just typography, whitespace, and thin dividers."
        }
    };
}