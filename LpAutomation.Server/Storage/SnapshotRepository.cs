using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace LpAutomation.Server.Storage;

public sealed class SnapshotRepository
{
    private readonly string _connectionString;

    public SnapshotRepository(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        _connectionString = cs;
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    // =========================
    // Raw snapshots
    // =========================

    public async Task InsertSnapshotAsync(PoolSnapshot s, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO pool_snapshots
(chain_id, pool_address, ts_utc, block_number, price, liquidity, volume_token0, volume_token1)
VALUES
(@ChainId, @PoolAddress, @TsUtc, @BlockNumber, @Price, @Liquidity, @VolumeToken0, @VolumeToken1);";

        var args = new
        {
            s.ChainId,
            PoolAddress = s.PoolAddress.ToLowerInvariant(),
            TsUtc = s.TimestampUtc.ToUniversalTime().ToString("O"),
            s.BlockNumber,
            s.Price,
            s.Liquidity,
            s.VolumeToken0,
            s.VolumeToken1
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PoolSnapshot>> GetSnapshotsAsync(
        int chainId,
        string poolAddress,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  chain_id      AS ChainId,
  pool_address  AS PoolAddress,
  ts_utc        AS TsUtc,
  block_number  AS BlockNumber,
  price         AS Price,
  liquidity     AS Liquidity,
  volume_token0 AS VolumeToken0,
  volume_token1 AS VolumeToken1
FROM pool_snapshots
WHERE chain_id = @ChainId
  AND pool_address = @PoolAddress
  AND ts_utc >= @FromUtc
  AND ts_utc <= @ToUtc
ORDER BY ts_utc ASC;";

        var args = new
        {
            ChainId = chainId,
            PoolAddress = poolAddress.ToLowerInvariant(),
            FromUtc = fromUtc.ToUniversalTime().ToString("O"),
            ToUtc = toUtc.ToUniversalTime().ToString("O")
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync(sql, args);

        var list = new List<PoolSnapshot>();
        foreach (var r in rows)
        {
            string ts = r.TsUtc;
            list.Add(new PoolSnapshot(
                (int)r.ChainId,
                (string)r.PoolAddress,
                DateTime.Parse(ts, null, DateTimeStyles.RoundtripKind),
                (long)r.BlockNumber,
                (double)r.Price,
                (double?)r.Liquidity,
                (double?)r.VolumeToken0,
                (double?)r.VolumeToken1
            ));
        }

        return list;
    }

    public async Task<long?> GetLatestBlockAsync(int chainId, string poolAddress, CancellationToken ct = default)
    {
        const string sql = @"
SELECT block_number
FROM pool_snapshots
WHERE chain_id = @ChainId AND pool_address = @PoolAddress
ORDER BY ts_utc DESC
LIMIT 1;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        return await conn.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new
            {
                ChainId = chainId,
                PoolAddress = poolAddress.ToLowerInvariant()
            }, cancellationToken: ct));
    }

    // Retention
    public async Task<int> PurgeOldSnapshotsAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM pool_snapshots
WHERE ts_utc < @CutoffUtc;";

        var args = new { CutoffUtc = olderThanUtc.ToUniversalTime().ToString("O") };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    // =========================
    // Bars (5m)
    // =========================

    public async Task<int> PurgeOldBars5mAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        const string sql = @"
DELETE FROM pool_bars_5m
WHERE ts_utc < @CutoffUtc;";

        var args = new { CutoffUtc = olderThanUtc.ToUniversalTime().ToString("O") };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task UpsertBar5mAsync(PriceBar bar, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO pool_bars_5m
(chain_id, pool_address, ts_utc, open, high, low, close, samples)
VALUES
(@ChainId, @PoolAddress, @TsUtc, @Open, @High, @Low, @Close, @Samples)
ON CONFLICT(chain_id, pool_address, ts_utc)
DO UPDATE SET
  open    = excluded.open,
  high    = excluded.high,
  low     = excluded.low,
  close   = excluded.close,
  samples = excluded.samples;";

        var args = new
        {
            bar.ChainId,
            PoolAddress = bar.PoolAddress.ToLowerInvariant(),
            TsUtc = bar.TimestampUtc.ToUniversalTime().ToString("O"),
            bar.Open,
            bar.High,
            bar.Low,
            bar.Close,
            bar.Samples
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PriceBar>> GetBars5mAsync(
        int chainId,
        string poolAddress,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  chain_id     AS ChainId,
  pool_address AS PoolAddress,
  ts_utc       AS TsUtc,
  open         AS Open,
  high         AS High,
  low          AS Low,
  close        AS Close,
  samples      AS Samples
FROM pool_bars_5m
WHERE chain_id = @ChainId
  AND pool_address = @PoolAddress
  AND ts_utc >= @FromUtc
  AND ts_utc <= @ToUtc
ORDER BY ts_utc ASC;";

        var args = new
        {
            ChainId = chainId,
            PoolAddress = poolAddress.ToLowerInvariant(),
            FromUtc = fromUtc.ToUniversalTime().ToString("O"),
            ToUtc = toUtc.ToUniversalTime().ToString("O")
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync(sql, args);

        var list = new List<PriceBar>();
        foreach (var r in rows)
        {
            string ts = r.TsUtc;

            list.Add(new PriceBar(
                (int)r.ChainId,
                (string)r.PoolAddress,
                DateTime.Parse(ts, null, DateTimeStyles.RoundtripKind),
                (double)r.Open,
                (double)r.High,
                (double)r.Low,
                (double)r.Close,
                (int)r.Samples
            ));
        }

        return list;
    }

    // =========================
    // Bars + returns helpers
    // =========================

    public enum BarInterval { M1, M5 }

    public sealed record BarClosePoint(DateTime TimestampUtc, double Close);

    public sealed record LogReturnPoint(DateTime TimestampUtc, double LogReturn);

    public async Task<IReadOnlyList<string>> GetKnownPoolsAsync(int chainId, CancellationToken ct = default)
    {
        // Pull pool ids from snapshots AND bars so correlation can work even if snapshots are purged.
        const string sql = @"
    SELECT DISTINCT pool_address
    FROM (
        SELECT pool_address FROM pool_snapshots WHERE chain_id = @ChainId
        UNION
        SELECT pool_address FROM pool_bars_5m    WHERE chain_id = @ChainId
        UNION
        SELECT pool_address FROM pool_bars_1m    WHERE chain_id = @ChainId
    )
    ORDER BY pool_address ASC;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync<string>(
            new CommandDefinition(sql, new { ChainId = chainId }, cancellationToken: ct));

        return rows.Select(x => x.ToLowerInvariant()).ToList();
    }

    public async Task<IReadOnlyList<BarClosePoint>> GetBarClosesAsync(
        int chainId,
        string poolAddress,
        BarInterval interval,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var table = interval switch
        {
            BarInterval.M1 => "pool_bars_1m",
            _ => "pool_bars_5m"
        };

        var sql = $@"
SELECT
  ts_utc AS TsUtc,
  close  AS Close
FROM {table}
WHERE chain_id = @ChainId
  AND pool_address = @PoolAddress
  AND ts_utc >= @FromUtc
  AND ts_utc <= @ToUtc
ORDER BY ts_utc ASC;";

        var args = new
        {
            ChainId = chainId,
            PoolAddress = poolAddress.ToLowerInvariant(),
            FromUtc = fromUtc.ToUniversalTime().ToString("O"),
            ToUtc = toUtc.ToUniversalTime().ToString("O")
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync(sql, args);

        var list = new List<BarClosePoint>();
        foreach (var r in rows)
        {
            string ts = r.TsUtc;
            double close = (double)r.Close;

            list.Add(new BarClosePoint(
                DateTime.Parse(ts, null, DateTimeStyles.RoundtripKind),
                close
            ));
        }

        return list;
    }

    public async Task<IReadOnlyList<LogReturnPoint>> GetLogReturnsAsync(
        int chainId,
        string poolAddress,
        BarInterval interval,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var closes = await GetBarClosesAsync(chainId, poolAddress, interval, fromUtc, toUtc, ct);

        if (closes.Count < 2)
            return Array.Empty<LogReturnPoint>();

        var rets = new List<LogReturnPoint>(closes.Count - 1);

        for (int i = 1; i < closes.Count; i++)
        {
            var p0 = closes[i - 1].Close;
            var p1 = closes[i].Close;
            if (p0 <= 0 || p1 <= 0) continue;

            var lr = Math.Log(p1 / p0);
            rets.Add(new LogReturnPoint(closes[i].TimestampUtc, lr));
        }

        return rets;
    }
}