using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

/// <summary>
/// Handles the "Sign in with Google" flow: opens the system browser to the
/// Cloudflare Worker's /auth/google route, listens on a local loopback port for
/// the redirect back, and exchanges the one-time code the Worker hands back for
/// the real user/session record over a normal HTTPS request.
///
/// Deliberately different from a naive version in a few ways:
///  - A random `state` value is generated per login attempt and must match on
///    the way back, so another local process can't spoof the redirect.
///  - Only a short-lived `code` travels through the browser/query string —
///    never the user's actual data or a long-lived token. The real payload is
///    fetched via HTTPS from the Worker, which keeps it out of browser history.
///  - The wait has a timeout, so the app never hangs forever if the user closes
///    the tab or denies access.
///  - The listener is always stopped, even if something throws.
/// </summary>
public class GoogleAuthService : IGoogleAuthService
{
    private const string WorkerBaseUrl = "https://broken-fog-91af.ustefan06.workers.dev";

    // route is configured to redirect back to.
    private const string LoopbackPrefix = "http://localhost:5050/";
    private const string GoogleLoginUrl = "https://broken-fog-91af.ustefan06.workers.dev/auth/google?state=test";

    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(3);

    private readonly HttpClient _httpClient;
    private readonly string _sessionFilePath;

    public bool IsLoggingIn { get; private set; }

    public GoogleAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // Same AppData folder your UserSettingsService already uses.
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(myAppFolder);
        _sessionFilePath = Path.Combine(myAppFolder, "auth-session.json");
    }

#pragma warning disable CA1416 // SecretProtector is Windows-only; this app targets Windows (WinExe).
    public async Task<GoogleAuthResult?> TryRestoreSessionAsync()
    {
        if (!File.Exists(_sessionFilePath))
            return null;

        try
        {
            string stored = await File.ReadAllTextAsync(_sessionFilePath);
            string json = SecretProtector.Unprotect(stored);
            var cached = JsonSerializer.Deserialize<GoogleAuthResult>(json);

            if (cached is null || string.IsNullOrEmpty(cached.SessionToken))
                return null;

            var expires = DateTimeOffset.FromUnixTimeSeconds(cached.ExpiresAt);
            if (expires < DateTimeOffset.UtcNow.AddMinutes(1))
                return null; // expired (or about to be) — fall back to a real login

            return cached;
        }
        catch
        {
            // Corrupted file, different machine/user, etc. — just ask them to log in again.
            return null;
        }
    }

    private async Task SaveSessionAsync(GoogleAuthResult result)
    {
        string json = JsonSerializer.Serialize(result);
        string protectedJson = SecretProtector.Protect(json);
        await File.WriteAllTextAsync(_sessionFilePath, protectedJson);
    }
#pragma warning restore CA1416

    public void Logout()
    {
        if (File.Exists(_sessionFilePath))
            File.Delete(_sessionFilePath);
    }

    public async Task<GoogleAuthResult> LoginWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoggingIn)
            throw new InvalidOperationException("A sign-in is already in progress.");

        IsLoggingIn = true;
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add(LoopbackPrefix);

        try
        {
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    "Could not start the local sign-in listener on port 5050. " +
                    "Close any other sign-in attempt and try again.", ex);
            }

            System.Diagnostics.Debug.WriteLine($"[AUTH DEBUG] client-generated state = {state}");

            Process.Start(new ProcessStartInfo
            {
                FileName = $"{GoogleLoginUrl}?state={state}",
                UseShellExecute = true
            });

            var context = await WaitForRedirectAsync(listener, cancellationToken);

            string? returnedState = context.Request.QueryString["state"];
            string? code = context.Request.QueryString["code"];
            string? error = context.Request.QueryString["error"];

            bool ok = error is null && !string.IsNullOrEmpty(code)
                      && string.Equals(returnedState, state, StringComparison.Ordinal);

            await RespondToBrowserAsync(context, ok);

            if (!ok)
            {
                throw new InvalidOperationException(error is not null
                    ? $"Google sign-in failed: {error}"
                    : "The sign-in response could not be verified. Please try again.");
            }

            // Exchange the one-time code for the real user record over HTTPS.
            // The browser/query string only ever sees the opaque `code`, never
            // the account data or a usable token.
            var exchangeUrl = $"{WorkerBaseUrl}/auth/exchange?code={Uri.EscapeDataString(code!)}&state={Uri.EscapeDataString(state)}";
            var response = await _httpClient.GetAsync(exchangeUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GoogleAuthResult>(cancellationToken: cancellationToken);
            if (result is null || string.IsNullOrEmpty(result.SessionToken))
                throw new InvalidOperationException("The server did not return a valid session.");

            await SaveSessionAsync(result);
            return result;
        }
        finally
        {
            IsLoggingIn = false;
            if (listener.IsListening)
                listener.Stop();
        }
    }

    private static async Task<HttpListenerContext> WaitForRedirectAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(LoginTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var contextTask = listener.GetContextAsync();
        var cancelTcs = new TaskCompletionSource();

        await using (linkedCts.Token.Register(() => cancelTcs.TrySetResult()))
        {
            var finished = await Task.WhenAny(contextTask, cancelTcs.Task);
            if (finished != contextTask)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                throw new TimeoutException("Sign-in timed out. Please try again.");
            }
        }

        return await contextTask;
    }

    private static async Task RespondToBrowserAsync(HttpListenerContext context, bool success)
    {
        // TEMP DEBUG: shows exactly what came back so we can see what mismatched.
        // Remove this once the state/code issue is found.
        string debugQuery = System.Net.WebUtility.HtmlEncode(context.Request.Url?.Query ?? "(none)");

        string html = success
            ? "<html><body style=\"font-family:sans-serif;text-align:center;padding-top:80px;\">" +
              "<h2>Login successful!</h2><p>You can close this tab and return to Obcred.</p></body></html>"
            : "<html><body style=\"font-family:sans-serif;text-align:center;padding-top:80px;\">" +
              "<h2>Login could not be verified.</h2><p>Please close this tab and try again in Obcred.</p>" +
              $"<p style=\"color:#999;font-size:12px;word-break:break-all;\">{debugQuery}</p></body></html>";

        byte[] bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}