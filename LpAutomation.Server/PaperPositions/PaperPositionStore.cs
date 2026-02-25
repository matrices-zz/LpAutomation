using System.Collections.Concurrent;
using LpAutomation.Contracts.PaperPositions;

namespace LpAutomation.Server.PaperPositions;

public interface IPaperPositionStore
{
    IReadOnlyList<PaperPositionDto> List(string? ownerTag = null, int take = 200);
    PaperPositionDto? Get(Guid id);
    PaperPositionDto Upsert(Guid? id, UpsertPaperPositionRequest req);
    bool Delete(Guid id);
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
        var normalizedToken0 = (req.Token0Symbol ?? "").Trim().ToUpperInvariant();
        var normalizedToken1 = (req.Token1Symbol ?? "").Trim().ToUpperInvariant();

        // Keep token pair canonical for stable identity
        if (string.CompareOrdinal(normalizedToken0, normalizedToken1) > 0)
            (normalizedToken0, normalizedToken1) = (normalizedToken1, normalizedToken0);

        var positionId = id ?? Guid.NewGuid();

        var openedUtc = _map.TryGetValue(positionId, out var existing)
            ? existing.OpenedUtc
            : now;

        var row = new PaperPositionDto(
            PositionId: positionId,
            OwnerTag: (req.OwnerTag ?? "").Trim(),
            ChainId: req.ChainId,
            Dex: (req.Dex ?? "").Trim(),
            PoolAddress: (req.PoolAddress ?? "").Trim(),
            Token0Symbol: normalizedToken0,
            Token1Symbol: normalizedToken1,
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
}
