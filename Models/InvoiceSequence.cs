using SQLite;

namespace Obcred.Models;

/// <summary>
/// The gap-free invoice counter, one row per calendar year.
/// <see cref="NextValue"/> is the number that will be assigned to the next
/// successfully issued invoice. It only advances when UJP accepts a submission,
/// so failed/rejected attempts never consume a number (no gaps in the sequence).
/// </summary>
public class InvoiceSequence
{
    [PrimaryKey]
    public int Year { get; set; }

    public int NextValue { get; set; } = 1;
}
