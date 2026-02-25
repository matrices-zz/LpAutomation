using System.Collections.Concurrent;
using LpAutomation.Contracts.PaperPositions;

namespace LpAutomation.Server.PaperPositions;

public interface IPaperPositionStore
{
    IReadOnlyList<PaperPositionDto> List(string? ownerTag = null, int take = 200);
    PaperPositionDto? Get(Guid id);
    PaperPositionDto Upsert(Guid? id, UpsertPaperPositionRequest req);
    bool Delete(Guid id);

    // NEW: helper for recommendation enrichment
    PaperPositionDto? FindBestMatch(
        string? ownerTag,
        int chainId,
        string token0Symbol,
        string token1Symbol,
        int feeTier);
}

public sealed class InMemoryPaperPositionStore : IPaperPositionStore
{
    private readonly ConcurrentDictionary<Guid, PaperPositionDto> _map = new();

    public IReadOnlyList<PaperPositionDto> List(string? ownerTag = null, int take = 200)
    {
        var query = _map.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(ownerTag))
            query = query.Where(x => string.Equals(x.OwnerTag, ownerTag, StringComparison.OrdinalIgnoreCase));

        return query
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(Math.Clamp(take, 1, 2000))
            .ToList();
    }

    public PaperPositionDto? Get(Guid id)
        => _map.TryGetValue(id, out var row) ? row : null;

    public PaperPositionDto Upsert(Guid? id, UpsertPaperPositionRequest req)
    {
        var now = DateTimeOffset.UtcNow;

        var ownerTag = (req.OwnerTag ?? "").Trim();
        var dex = (req.Dex ?? "").Trim();

        var token0 = (req.Token0Symbol ?? "").Trim().ToUpperInvariant();
        var token1 = (req.Token1Symbol ?? "").Trim().ToUpperInvariant();

        // Canonicalize token ordering for stable matching
        if (string.CompareOrdinal(token0, token1) > 0)
            (token0, token1) = (token1, token0);

        var positionId = id ?? Guid.NewGuid();
        var openedUtc = _map.TryGetValue(positionId, out var existing)
            ? existing.OpenedUtc
            : now;

        var row = new PaperPositionDto(
            PositionId: positionId,
            OwnerTag: ownerTag,
            ChainId: req.ChainId,
            Dex: dex,
            PoolAddress: (req.PoolAddress ?? "").Trim(),
            Token0Symbol: token0,
            Token1Symbol: token1,
            FeeTier: req.FeeTier,
            LiquidityNotionalUsd: req.LiquidityNotionalUsd,
            EntryPrice: req.EntryPrice,
            TickLower: req.TickLower,
            TickUpper: req.TickUpper,
            OpenedUtc: openedUtc,
            UpdatedUtc: now,
            Enabled: req.Enabled,
            Notes: req.Notes
        );

        _map[positionId] = row;
        return row;
    }

    public bool Delete(Guid id)
        => _map.TryRemove(id, out _);

    public PaperPositionDto? FindBestMatch(
        string? ownerTag,
        int chainId,
        string token0Symbol,
        string token1Symbol,
        int feeTier)
    {
        var a = (token0Symbol ?? "").Trim().ToUpperInvariant();
        var b = (token1Symbol ?? "").Trim().ToUpperInvariant();
        if (string.CompareOrdinal(a, b) > 0)
            (a, b) = (b, a);

        var q = _map.Values.Where(x =>
            x.Enabled &&
            x.ChainId == chainId &&
            x.FeeTier == feeTier &&
            string.Equals(x.Token0Symbol, a, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Token1Symbol, b, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(ownerTag))
            q = q.Where(x => string.Equals(x.OwnerTag, ownerTag, StringComparison.OrdinalIgnoreCase));

        // If multiple match, pick most recently updated
        return q.OrderByDescending(x => x.UpdatedUtc).FirstOrDefault();
    }
}
