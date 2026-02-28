using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LpAutomation.Contracts.Config;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class ConfigApiClient
{
    private readonly HttpClient _http;

    public ConfigApiClient(HttpClient http) => _http = http;

    public async Task<string> GetCurrentRawAsync()
    {
        using var resp = await _http.GetAsync("api/config/current");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }

    public async Task<IReadOnlyList<ActivePoolDto>> GetActivePoolsAsync(CancellationToken ct = default)
    {
        var rows = await _http.GetFromJsonAsync<List<ActivePoolDto>>("api/config/active-pools", cancellationToken: ct);
        return rows ?? new List<ActivePoolDto>();
    }

    // UPDATED: Uses PUT and the correct route
    public async Task UpdateAsync(string jsonUpdate)
    {
        // Your server expects a ConfigPutRequest object: { "config": { ... } }
        // We wrap the incoming JSON string into that structure
        var wrappedJson = $"{{\"config\": {jsonUpdate}}}";

        var content = new StringContent(wrappedJson, Encoding.UTF8, "application/json");

        // Change from PostAsync("api/config/update") to PutAsync("api/config/current")
        using var resp = await _http.PutAsync("api/config/current", content);

        resp.EnsureSuccessStatusCode();
    }
}