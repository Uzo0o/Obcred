using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public class UsageService : IUsageService
{
    private const string WorkerBaseUrl = "https://broken-fog-91af.ustefan06.workers.dev";

    private readonly HttpClient _httpClient;
    private readonly ISessionContext _sessionContext;
    private readonly string _overageAckPath;

    public UsageService(HttpClient httpClient, ISessionContext sessionContext)
    {
        _httpClient = httpClient;
        _sessionContext = sessionContext;

        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(myAppFolder);
        _overageAckPath = Path.Combine(myAppFolder, "overage-ack.json");
    }

    public async Task IncrementAsync()
    {
        string? token = _sessionContext.Current?.SessionToken;
        if (string.IsNullOrEmpty(token))
            return; // not logged in yet — shouldn't happen post-login, but never worth blocking a submit over

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkerBaseUrl}/usage/increment");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await _httpClient.SendAsync(request);
        }
        catch
        {
            // Best-effort only — see IUsageService.IncrementAsync remarks.
        }
    }

    public async Task<UsageStatus?> GetStatusAsync()
    {
        string? token = _sessionContext.Current?.SessionToken;
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{WorkerBaseUrl}/usage/status");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<UsageStatus>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<PlanInfo>?> GetPlansAsync()
    {
        string? token = _sessionContext.Current?.SessionToken;
        if (string.IsNullOrEmpty(token))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{WorkerBaseUrl}/plans");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return null;

            var result = await response.Content.ReadFromJsonAsync<PlansResponse>();
            return result?.Plans;
        }
        catch
        {
            return null;
        }
    }

    public async Task<PlanSelectResult> SelectPlanAsync(string planId)
    {
        string? token = _sessionContext.Current?.SessionToken;
        if (string.IsNullOrEmpty(token))
            return new PlanSelectResult { Success = false, ErrorMessage = "You're not logged in." };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkerBaseUrl}/plan/select");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { plan = planId });

            using var response = await _httpClient.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var err = JsonSerializer.Deserialize<PlanSelectErrorBody>(body);
                return new PlanSelectResult
                {
                    Success = false,
                    DowngradeRejected = true,
                    Plan = err?.CurrentPlan,
                    ErrorMessage = err?.Message ?? "You can't move to a lower plan until next month."
                };
            }

            if (!response.IsSuccessStatusCode)
                return new PlanSelectResult { Success = false, ErrorMessage = $"Server error ({(int)response.StatusCode})." };

            var ok = JsonSerializer.Deserialize<PlanSelectOkBody>(body);
            return new PlanSelectResult
            {
                Success = true,
                Plan = ok?.Plan,
                Limit = ok?.Limit,
                Price = ok?.Price
            };
        }
        catch (Exception ex)
        {
            return new PlanSelectResult { Success = false, ErrorMessage = $"Network error: {ex.Message}" };
        }
    }

    public async Task<bool> SyncEdbAsync(string edb)
    {
        string? token = _sessionContext.Current?.SessionToken;
        if (string.IsNullOrEmpty(token) || string.IsNullOrWhiteSpace(edb))
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{WorkerBaseUrl}/me/edb");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { edb });

            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false; // best-effort — never block Settings from completing over this
        }
    }

    public bool HasAcknowledgedOverageThisMonth()
    {
        try
        {
            if (!File.Exists(_overageAckPath))
                return false;

            var data = JsonSerializer.Deserialize<OverageAck>(File.ReadAllText(_overageAckPath));
            return data?.YearMonth == DateTime.UtcNow.ToString("yyyy-MM");
        }
        catch
        {
            return false; // if we can't tell, err on the side of showing the prompt again
        }
    }

    public void AcknowledgeOverageThisMonth()
    {
        try
        {
            var data = new OverageAck { YearMonth = DateTime.UtcNow.ToString("yyyy-MM") };
            File.WriteAllText(_overageAckPath, JsonSerializer.Serialize(data));
        }
        catch
        {
            // Best-effort — worst case the prompt shows again next submit.
        }
    }

    private class OverageAck
    {
        public string YearMonth { get; set; } = string.Empty;
    }

    private class PlanSelectErrorBody
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")] public string? Message { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("currentPlan")] public string? CurrentPlan { get; set; }
    }

    private class PlanSelectOkBody
    {
        [System.Text.Json.Serialization.JsonPropertyName("plan")] public string? Plan { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("limit")] public int? Limit { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("price")] public int? Price { get; set; }
    }
}