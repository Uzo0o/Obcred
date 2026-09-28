using System.Collections.Generic;
using System.Linq;

namespace Obcred.Models;

/// <summary>
/// One accent color a PDF template can be rendered in: a dark shade (header backgrounds,
/// strong emphasis), a mid shade (highlighted numbers, section labels) and a light shade
/// (soft tint backgrounds/zebra striping). Neutral grays elsewhere in the templates
/// (borders, muted labels) stay fixed regardless of which accent is picked.
/// </summary>
public class PdfAccentColor
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Dark { get; set; } = string.Empty;
    public string Mid { get; set; } = string.Empty;
    public string Light { get; set; } = string.Empty;

    public static readonly IReadOnlyList<PdfAccentColor> All = new List<PdfAccentColor>
    {
        new() { Id = "Blue", Name = "Сина", Dark = "#1B3A6B", Mid = "#2E86FF", Light = "#F4F7FC" },
        new() { Id = "Red", Name = "Црвена", Dark = "#7A1620", Mid = "#E53E3E", Light = "#FDF2F2" },
        new() { Id = "Green", Name = "Зелена", Dark = "#14532D", Mid = "#16A34A", Light = "#F0FDF4" },
        new() { Id = "Amber", Name = "Жолта", Dark = "#78350F", Mid = "#D97706", Light = "#FFFBEB" },
        new() { Id = "Purple", Name = "Виолетова", Dark = "#4C1D95", Mid = "#7C3AED", Light = "#F5F3FF" },
        new() { Id = "Teal", Name = "Тиркизна", Dark = "#134E4A", Mid = "#0D9488", Light = "#F0FDFA" },
        new() { Id = "Pink", Name = "Розова", Dark = "#831843", Mid = "#DB2777", Light = "#FDF2F8" },
        new() { Id = "Graphite", Name = "Графитна", Dark = "#1E293B", Mid = "#475569", Light = "#F8FAFC" },
    };

    public static PdfAccentColor FromId(string? id) =>
        All.FirstOrDefault(c => c.Id == id) ?? All[0];
}
