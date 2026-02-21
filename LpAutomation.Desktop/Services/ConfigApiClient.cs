using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LpAutomation.Core.Models;
using LpAutomation.Core.Serialization;
using LpAutomation.Contracts.Config;

namespace LpAutomation.Desktop.Services;

public sealed class ConfigApiClient
{
    private readonly HttpClient _http;
    public ConfigApiClient(HttpClient http) => _http = http;

    public async Task<ConfigGetResponse> GetCurrentAsync()
        => (await _http.GetFromJsonAsync<ConfigGetResponse>("api/config/current", JsonStrict.Options))!;

    public async Task<ConfigValidateResponse> ValidateAsync(StrategyConfigDocument cfg)
    {
        var resp = await _http.PostAsJsonAsync("api/config/validate", new ConfigPutRequest(cfg), JsonStrict.Options);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConfigValidateResponse>(JsonStrict.Options))!;
    }

    public async Task<ConfigGetResponse> SaveAsync(StrategyConfigDocument cfg)
    {
        var resp = await _http.PutAsJsonAsync("api/config/current", new ConfigPutRequest(cfg), JsonStrict.Options);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConfigGetResponse>(JsonStrict.Options))!;
    }

    public async Task<string> ExportJsonAsync() => await _http.GetStringAsync("api/config/export");

    public async Task<ConfigGetResponse> ImportJsonAsync(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/config/import", content);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ConfigGetResponse>(JsonStrict.Options))!;
    }

    public async Task<ConfigHistoryItem[]> HistoryAsync(int take = 50)
        => (await _http.GetFromJsonAsync<ConfigHistoryItem[]>($"api/config/history?take={take}", JsonStrict.Options))!;

    public async Task<StrategyConfigDocument> GetVersionAsync(long id)
        => (await _http.GetFromJsonAsync<StrategyConfigDocument>($"api/config/version/{id}", JsonStrict.Options))!;
}
