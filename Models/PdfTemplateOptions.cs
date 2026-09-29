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
            DisplayName = "Класичен",
            Description = "Оградени полиња за продавач/купувач, чиста табела со линии. Традиционален и сигурен."
        },
        new()
        {
            Id = "Modern",
            DisplayName = "Модерен",
            Description = "Акцентирана лента во заглавието со вашето лого, засенчени редови во табелата, поизразен блок со вкупни износи."
        },
        new()
        {
            Id = "Minimal",
            DisplayName = "Минималистички",
            Description = "Без рамки или засенчување — само типографија, празен простор и тенки разделители."
        },
        new()
        {
            Id = "Custom",
            DisplayName = "Прилагоден (од вашата фактура)",
            Description = "Го користи заглавието и подножјето увезени од вашата PDF фактура како позадина — достапно откако ќе увезете образец."
        }
    };
}