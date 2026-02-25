using System;

namespace LpAutomation.Contracts.PaperPositions;

public sealed record PaperPositionDto(
    Guid PositionId,
    string OwnerTag,
    int ChainId,
    string Dex,
    string PoolAddress,
    string Token0Symbol,
    string Token1Symbol,
    int FeeTier,
    decimal LiquidityNotionalUsd,
    decimal EntryPrice,
    int TickLower,
    int TickUpper,
    DateTimeOffset OpenedUtc,
    DateTimeOffset UpdatedUtc,
    bool Enabled,
    string? Notes
);

public sealed record UpsertPaperPositionRequest(
    string OwnerTag,
    int ChainId,
    string Dex,
    string PoolAddress,
    string Token0Symbol,
    string Token1Symbol,
    int FeeTier,
    decimal LiquidityNotionalUsd,
    decimal EntryPrice,
    int TickLower,
    int TickUpper,
    bool Enabled,
    string? Notes
);

public sealed record UpsertPaperPositionResponse(PaperPositionDto Position);
