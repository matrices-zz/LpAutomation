using System;

namespace LpAutomation.Contracts.PaperPositions;

/// <summary>
/// Represents a synthetic/paper LP position used for testing decision logic
/// before wiring real wallet/NFT position discovery.
/// </summary>
public sealed record PaperPositionDto(
    Guid PositionId,
    string OwnerTag,                 // e.g. "eddie-dev", "demo-ledger"
    int ChainId,                     // 1=ETH, 8453=Base, 42161=Arbitrum, etc.
    string Dex,                      // e.g. "UniswapV3"
    string PoolAddress,              // e.g. 0x88e6...
    string Token0Symbol,             // e.g. "USDC"
    string Token1Symbol,             // e.g. "WETH"
    int FeeTier,                     // 500/3000/10000
    decimal LiquidityNotionalUsd,    // synthetic position size
    decimal EntryPrice,              // synthetic entry reference
    int TickLower,                   // synthetic range lower
    int TickUpper,                   // synthetic range upper
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

public sealed record UpsertPaperPositionResponse(
    PaperPositionDto Position
);
