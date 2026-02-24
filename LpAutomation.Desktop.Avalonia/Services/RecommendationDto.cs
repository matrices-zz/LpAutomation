using System;

namespace LpAutomation.Desktop.Avalonia.Services;

public sealed class RecommendationDto
{
    public string Id { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public int ChainId { get; set; }
    public string Token0 { get; set; } = "";
    public string Token1 { get; set; } = "";
    public int FeeTier { get; set; }
    public int Regime { get; set; }
    public int ReinvestScore { get; set; }
    public int ReallocateScore { get; set; }
    public string Summary { get; set; } = "";
    public string? DetailsJson { get; set; }

    public string Dex { get; set; } = "";
    public string ProtocolVersion { get; set; } = "";
    public string? Token0Address { get; set; }
    public string? Token1Address { get; set; }
    public string? PoolAddress { get; set; }
}
