using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
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
}
