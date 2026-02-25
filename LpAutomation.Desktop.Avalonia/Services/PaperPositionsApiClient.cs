using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LpAutomation.Contracts.PaperPositions;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class PaperPositionsApiClient
{
    private readonly HttpClient _http;

    public PaperPositionsApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<PaperPositionDto>> ListAsync(string? ownerTag = null, int take = 200, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(ownerTag)
            ? $"api/paper-positions?take={take}"
            : $"api/paper-positions?ownerTag={Uri.EscapeDataString(ownerTag)}&take={take}";

        var rows = await _http.GetFromJsonAsync<List<PaperPositionDto>>(query, cancellationToken: ct);
        return rows ?? new List<PaperPositionDto>();
    }

    public async Task<PaperPositionDto> CreateAsync(UpsertPaperPositionRequest req, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("api/paper-positions", req, ct);
        resp.EnsureSuccessStatusCode();

        var wrapped = await resp.Content.ReadFromJsonAsync<UpsertPaperPositionResponse>(cancellationToken: ct);
        return wrapped?.Position ?? throw new InvalidOperationException("Create returned no position payload.");
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync($"api/paper-positions/{id}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        resp.EnsureSuccessStatusCode();
        return true;
    }
}
