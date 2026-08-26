using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public class UsageService : IUsageService
{
    private const string WorkerBaseUrl = "https://broken-fog-91af.ustefan06.workers.dev";

    private readonly HttpClient _httpClient;
    private readonly ISessionContext _sessionContext;

    public UsageService(HttpClient httpClient, ISessionContext sessionContext)
    {
        _httpClient = httpClient;
        _sessionContext = sessionContext;
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
}