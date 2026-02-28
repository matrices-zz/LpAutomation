namespace LpAutomation.Server.Storage;

public sealed record PoolSnapshot(
    int ChainId,
    string PoolAddress,
    DateTime TimestampUtc,
    long BlockNumber,
    double Price,
    double? Liquidity = null,
    double? VolumeToken0 = null,
    double? VolumeToken1 = null,
    string? Source = null,
    int? LatencyMs = null,
    string? FinalityStatus = null,
    string? QualityFlags = null
);