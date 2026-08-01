using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Tasks;
using Jose;
using Obcred.Models;

namespace Obcred.Services;

public class UjpService : IUjpService
{
    private readonly HttpClient _baseHttpClient;
    private readonly IUserSettingsService _settingsService;

    private const string TestBaseUrl = "https://efakturatest.ujp.gov.mk";
    // NOTE: verify the exact production host against the UJP documentation before going live.
    private const string ProductionBaseUrl = "https://efakturatest.ujp.gov.mk";

    // Chosen per-request from the user's setting, so switching environments takes effect immediately.
    private string BaseUrl =>
        _settingsService.CurrentSettings.UseProductionEnvironment ? ProductionBaseUrl : TestBaseUrl;

    public UjpService(HttpClient httpClient, IUserSettingsService settingsService)
    {
        _baseHttpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<string> GetServerTimestampAsync()
    {
        var response = await _baseHttpClient.GetAsync($"{BaseUrl}/einvoice_api/api/v1/server-time");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ServerTimeResponse>();
        
        if (result == null || string.IsNullOrWhiteSpace(result.Timestamp))
        {
            throw new Exception("Failed to parse the server timestamp from UJP.");
        }

        return result.Timestamp;
    }

    // Add this helper class at the bottom near your CompanyResponse class
    internal class ServerTimeResponse
    {
        [JsonPropertyName("timestamp")] 
        public string Timestamp { get; set; }
    }

    // Company lookup works over standard HTTP without transport certs
    public async Task<CompanyDto> GetCompanyDetailsAsync(string edb)
    {
        var settings = _settingsService.CurrentSettings;
        using X509Certificate2 cert = GetUserCertificate();
        
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri(BaseUrl);

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-EDB", settings.SellerEdb);
        client.DefaultRequestHeaders.Add("X-EUJP-ID", settings.EujpId);

        string path = $"einvoice_api/api/v1/companies/{edb}";

        var response = await client.GetAsync(path);
        
        // THE UTF-8 FIX: Read raw bytes and forcefully decode them as UTF-8
        byte[] contentBytes = await response.Content.ReadAsByteArrayAsync();
        string content = Encoding.UTF8.GetString(contentBytes);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"UJP Lookup Error {(int)response.StatusCode}: {content}");
        }

        var wrapper = JsonSerializer.Deserialize<CompanyResponse>(content, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
        return wrapper?.Company ?? throw new Exception("Failed to deserialize company data.");
    }

    public async Task<UjpSubmissionResult> SubmitInvoiceAsync(object invoice)
    {
        var settings = _settingsService.CurrentSettings;

        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
        string jsonString = JsonSerializer.Serialize(invoice, jsonOptions);

        // Fetch the certificate ONCE for this whole operation
        using X509Certificate2 cert = GetUserCertificate();

        // Sign using the SAME cert instance
        string signedJws = SignPayload(jsonString, cert);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);

        using var client = new HttpClient(handler);
        client.BaseAddress = new Uri(BaseUrl);

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("X-EDB", settings.SellerEdb);
        client.DefaultRequestHeaders.Add("X-EUJP-ID", settings.EujpId);
        client.DefaultRequestHeaders.Add("X-SERIAL-NUMBER", GetCorrectSerialNumber(cert));

        var wrapper = new { jws = signedJws };
        string payloadJson = JsonSerializer.Serialize(wrapper);
        var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        const string endpoint = "/JSONReceiver/api/v1/sales-invoices/send";
        var response = await client.PostAsync(endpoint, content);
        string responseContent = await response.Content.ReadAsStringAsync();

        // Always return a fully-populated result — including on rejection — so the
        // caller can persist the signed document and server response either way.
        return new UjpSubmissionResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            ResponseBody = responseContent,
            SignedJws = signedJws
        };
    }
    private static string GetCorrectSerialNumber(X509Certificate2 cert)
    {
        string hex = cert.SerialNumber; // already in the correct byte order
        hex = hex.TrimStart('0');
        if (string.IsNullOrEmpty(hex)) hex = "0"; // edge case: serial is literally zero
        return hex;
    }

    private X509Certificate2 GetUserCertificate()
    {
        var settings = _settingsService.CurrentSettings;

        if (!string.IsNullOrWhiteSpace(settings.CertThumbprint))
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByThumbprint, settings.CertThumbprint, false);
            if (certs.Count > 0) return new X509Certificate2(certs[0]);
            
            throw new Exception("USB Hardware Token missing from the local system store.");
        }

        if (!string.IsNullOrWhiteSpace(settings.CertPath))
        {
            // .NET 9+: the X509Certificate2 file constructor is obsolete (SYSLIB0057).
            // X509CertificateLoader is the supported way to load a PKCS#12 (.pfx) with its private key.
            return X509CertificateLoader.LoadPkcs12FromFile(settings.CertPath, settings.CertPassword);
        }

        throw new Exception("No active digital certificate identity configured.");
    }

    private string SignPayload(string jsonContent, X509Certificate2 cert)
    {
        var headers = new Dictionary<string, object> { { "alg", "RS256" }, { "typ", "JWT" } };
        using RSA privateKey = cert.GetRSAPrivateKey();

        if (privateKey == null)
            throw new Exception("The configured certificate does not contain an accessible RSA private key.");

        return JWT.Encode(jsonContent, privateKey, JwsAlgorithm.RS256, extraHeaders: headers);
    }

    internal class CompanyResponse
    {
        [JsonPropertyName("company")] public CompanyDto Company { get; set; }
    }
}