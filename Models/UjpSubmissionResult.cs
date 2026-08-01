namespace Obcred.Models;

/// <summary>
/// The outcome of a UJP submission. Unlike the old "return the body or throw"
/// approach, this always comes back populated — including on a rejection — so the
/// caller can persist the signed document and the exact server response either way.
/// </summary>
public class UjpSubmissionResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public string SignedJws { get; set; } = string.Empty;
}
