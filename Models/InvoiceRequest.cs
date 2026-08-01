using System.Text.Json.Serialization;

namespace Obcred.Models;

public class InvoiceRequest
{
    [JsonPropertyName("jws")] public string Jws { get; set; } // The wrapper required by the API
}