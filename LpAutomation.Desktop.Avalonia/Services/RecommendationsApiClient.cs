using System.Net.Http;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class RecommendationsApiClient
{
    private readonly HttpClient _http;

    public RecommendationsApiClient(HttpClient http) => _http = http;

    public async Task<string> GetLatestRawAsync(int take = 50)
    {
        using var resp = await _http.GetAsync($"api/recommendations?take={take}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync();
    }
}
