using System.Text.Json.Serialization;

namespace Obcred.Models;

/// <summary>
/// The user/session record returned by the Cloudflare Worker's /auth/exchange
/// endpoint after a successful Google sign-in.
///
/// NOTE: the [JsonPropertyName] values here are a reasonable guess based on a
/// typical OAuth exchange response. Adjust them to match whatever your Worker
/// actually returns (check the Worker's response body in your testing).
/// </summary>
public class GoogleAuthResult
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("picture")] public string PictureUrl { get; set; } = string.Empty;

    // The token the app should send back to the Worker (e.g. as
    // "Authorization: Bearer {SessionToken}") on subsequent API calls.
    [JsonPropertyName("sessionToken")] public string SessionToken { get; set; } = string.Empty;

    // Unix seconds. Lets us decide a cached session is still valid without
    // having to round-trip to the Worker just to check.
    [JsonPropertyName("expiresAt")] public long ExpiresAt { get; set; }
}