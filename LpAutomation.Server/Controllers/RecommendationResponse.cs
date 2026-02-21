namespace LpAutomation.Server.Contracts.Recommendations;

public sealed class RecommendationResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }

    public int ChainId { get; init; }
    public string Dex { get; init; } = "UniswapV3";   // or "PancakeV3" etc.
    public string ProtocolVersion { get; init; } = "v3"; // "v3" now, later "v4"

    // Human-friendly
    public string Token0Symbol { get; init; } = "";
    public string Token1Symbol { get; init; } = "";
    public int FeeTier { get; init; }

    // Canonical identity (critical)
    public string Token0Address { get; init; } = ""; // 0x...
    public string Token1Address { get; init; } = ""; // 0x...
    public string? PoolAddress { get; init; }        // 0x... (v3)

    // Outputs
    public int Regime { get; init; }
    public int ReinvestScore { get; init; }
    public int ReallocateScore { get; init; }
    public string Summary { get; init; } = "";
    public string? DetailsJson { get; init; }
}
