using LpAutomation.Server.Storage;

namespace LpAutomation.Server.Analytics;

public sealed class ReturnsFrameBuilder
{
    private readonly SnapshotRepository _repo;

    public ReturnsFrameBuilder(SnapshotRepository repo)
    {
        _repo = repo;
    }

    public sealed record ReturnsFrameResult(
        bool Ok,
        string Message,
        int Points,
        IReadOnlyList<string> Pools,
        IReadOnlyList<DateTime> TimestampsUtc,
        IReadOnlyList<double[]> Returns,
        IReadOnlyDictionary<string, int> PerPoolCounts
    );

    public async Task<ReturnsFrameResult> BuildIntersectionFrameWithDiagnosticsAsync(
        int chainId,
        IEnumerable<string> poolAddresses,
        SnapshotRepository.BarInterval interval,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Normalize input pools
        var pools = poolAddresses
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pools.Count < 2)
            return new ReturnsFrameResult(
                false,
                "Need at least 2 pools (use pools=... or ensure DB has >=2 pools).",
                0,
                pools,
                Array.Empty<DateTime>(),
                Array.Empty<double[]>(),
                new Dictionary<string, int>());

        // Fetch each pool's log-return series (bucket-normalized)
        // NOTE: We keep perPoolCounts for diagnostics even if we later drop pools.
        var seriesAll = new Dictionary<string, Dictionary<DateTime, double>>(StringComparer.OrdinalIgnoreCase);
        var perPool = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var pool in pools)
        {
            var rows = await _repo.GetLogReturnsAsync(chainId, pool, interval, fromUtc, toUtc, ct);

            var dict = new Dictionary<DateTime, double>(rows.Count);
            foreach (var r in rows)
            {
                var ts = NormalizeToBucketUtc(r.TimestampUtc, interval);
                dict[ts] = r.LogReturn;
            }

            seriesAll[pool] = dict;
            perPool[pool] = dict.Count;
        }

        // If any pool has zero returns, intersection will fail; we’ll rely on the drop logic below.
        // But if ALL pools are empty, stop early.
        if (perPool.All(kv => kv.Value == 0))
        {
            return new ReturnsFrameResult(
                false,
                "All pools have 0 return points. Ensure bars exist and increase lookback if needed.",
                0,
                pools,
                Array.Empty<DateTime>(),
                Array.Empty<double[]>(),
                perPool
            );
        }

        // ---- Institutional hygiene policy (Option A): drop sparsest pools until intersection is healthy ----
        // Heuristic target: require at least 60% of expected buckets, with a sensible floor and ceiling.
        var targetMinPoints = ComputeTargetMinPoints(interval, fromUtc, toUtc);

        var workingPools = pools.ToList();
        var dropped = new List<string>();

        while (workingPools.Count > 2)
        {
            var common = ComputeIntersectionTimestamps(seriesAll, workingPools);
            if (common.Count >= targetMinPoints)
                break;

            // Drop the pool with the fewest points (sparsest)
            var toDrop = workingPools
                .OrderBy(p => perPool.TryGetValue(p, out var c) ? c : 0)
                .First();

            workingPools.Remove(toDrop);
            dropped.Add(toDrop);
        }

        // Build final intersection for the selected pools
        var timestamps = ComputeIntersectionTimestamps(seriesAll, workingPools);

        if (timestamps.Count == 0)
        {
            return new ReturnsFrameResult(
                false,
                "Pools have return data but share 0 common timestamps. This usually means bar bucket timestamps are not rounded consistently. Fix bucketing to exact boundaries.",
                0,
                workingPools,
                Array.Empty<DateTime>(),
                Array.Empty<double[]>(),
                perPool
            );
        }

        // Keep the old minimum viability rule: under 5 points is too flimsy for correlation/matrices.
        if (timestamps.Count < 5)
        {
            var msg = $"Too few common aligned points ({timestamps.Count}). " +
                      $"Increase lookback or allow more time for bars to populate. " +
                      $"Selected pools: {string.Join(", ", workingPools)}.";

            if (dropped.Count > 0)
                msg += $" Dropped sparse pools: {string.Join(", ", dropped)}.";

            return new ReturnsFrameResult(
                false,
                msg,
                timestamps.Count,
                workingPools,
                timestamps,
                Array.Empty<double[]>(),
                perPool
            );
        }

        // Build aligned vectors (pool-major order)
        var aligned = new List<double[]>(workingPools.Count);
        foreach (var pool in workingPools)
        {
            var dict = seriesAll[pool];
            var vec = new double[timestamps.Count];
            for (int i = 0; i < timestamps.Count; i++)
                vec[i] = dict[timestamps[i]];
            aligned.Add(vec);
        }

        var okMsg = $"OK (intersection={timestamps.Count} points).";
        if (dropped.Count > 0)
            okMsg += $" Dropped sparse pools to meet quality threshold (target≥{targetMinPoints}): {string.Join(", ", dropped)}.";

        return new ReturnsFrameResult(
            true,
            okMsg,
            timestamps.Count,
            workingPools,
            timestamps,
            aligned,
            perPool
        );
    }

    private static int ComputeTargetMinPoints(SnapshotRepository.BarInterval interval, DateTime fromUtc, DateTime toUtc)
    {
        var minutesPerBucket = interval switch
        {
            SnapshotRepository.BarInterval.M1 => 1.0,
            SnapshotRepository.BarInterval.M5 => 5.0,
            _ => 5.0
        };

        var totalMinutes = Math.Max(0.0, (toUtc - fromUtc).TotalMinutes);
        var expected = (int)Math.Floor(totalMinutes / minutesPerBucket);

        // Require ~60% of expected buckets, with a reasonable floor/ceiling.
        // Floor keeps small lookbacks usable; ceiling avoids overly strict requirements for long windows.
        var target = (int)Math.Round(expected * 0.60, MidpointRounding.AwayFromZero);
        if (target < 20) target = 20;
        if (target > 200) target = 200;

        // The absolute minimum is still 5 for any meaningful math.
        if (target < 5) target = 5;

        return target;
    }

    private static List<DateTime> ComputeIntersectionTimestamps(
        IReadOnlyDictionary<string, Dictionary<DateTime, double>> seriesAll,
        IReadOnlyList<string> pools)
    {
        if (pools.Count == 0)
            return new List<DateTime>();

        var common = seriesAll[pools[0]].Keys.ToHashSet();
        for (int i = 1; i < pools.Count; i++)
            common.IntersectWith(seriesAll[pools[i]].Keys);

        return common.OrderBy(t => t).ToList();
    }

    private static DateTime NormalizeToBucketUtc(DateTime utc, SnapshotRepository.BarInterval interval)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        static DateTime TruncToMinute(DateTime u) =>
            new(u.Year, u.Month, u.Day, u.Hour, u.Minute, 0, DateTimeKind.Utc);

        static DateTime TruncTo5Minute(DateTime u)
        {
            var m = (u.Minute / 5) * 5;
            return new DateTime(u.Year, u.Month, u.Day, u.Hour, m, 0, DateTimeKind.Utc);
        }

        return interval switch
        {
            SnapshotRepository.BarInterval.M1 => TruncToMinute(utc),
            SnapshotRepository.BarInterval.M5 => TruncTo5Minute(utc),
            _ => TruncTo5Minute(utc)
        };
    }
}