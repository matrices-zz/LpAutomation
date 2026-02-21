using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Services;

public sealed class RecommendationsApiClient
{
    private readonly HttpClient _http;

    public RecommendationsApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<RecommendationDto>> GetRecommendationsAsync(int take = 200, CancellationToken ct = default)
    {
        var url = $"/api/recommendations?take={take}";
        var result = await _http.GetFromJsonAsync<List<RecommendationDto>>(url, ct);

        return result is { Count: > 0 } ? result : Array.Empty<RecommendationDto>();
    }
}

// Keep DTOs close to the client for now (you can move them later)
public sealed class RecommendationDto
{
    public string? Dex { get; set; }
    public string? ProtocolVersion { get; set; }
    public string? Token0Address { get; set; }
    public string? Token1Address { get; set; }
    public string? PoolAddress { get; set; }
    public string? Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }

    public int ChainId { get; set; }

    public string? Token0 { get; set; }
    public string? Token1 { get; set; }
    public int FeeTier { get; set; }

    public int Regime { get; set; } // 0=Sideways, 1=Trending, 2=Volatile (based on your sample)

    public int ReinvestScore { get; set; }
    public int ReallocateScore { get; set; }

    public string? Summary { get; set; }
    public string? DetailsJson { get; set; }
}
