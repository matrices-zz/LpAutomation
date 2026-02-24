using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class RecommendationsApiClient
{
    private readonly HttpClient _http;

    public RecommendationsApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(int take = 100, CancellationToken ct = default)
    {
        var url = $"api/recommendations?take={take}";
        var data = await _http.GetFromJsonAsync<List<RecommendationDto>>(url, cancellationToken: ct);
        return data ?? new List<RecommendationDto>();
    }


    // keep your raw call too if you still use it elsewhere
    public async Task<string> GetLatestRawAsync(int take = 50)
    {
        using var resp = await _http.GetAsync($"api/recommendations?take={take}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }
}
