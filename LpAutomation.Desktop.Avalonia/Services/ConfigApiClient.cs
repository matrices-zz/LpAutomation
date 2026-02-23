using System.Net.Http;
using System.Threading.Tasks;

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
}
