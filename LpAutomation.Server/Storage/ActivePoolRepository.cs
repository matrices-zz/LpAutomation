using Dapper;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace LpAutomation.Server.Storage;

public sealed class ActivePoolRepository
{
    private readonly string _connectionString;

    public ActivePoolRepository(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public sealed record ActivePoolRow(
        int ChainId,
        string Token0,
        string Token1,
        int FeeTier,
        string Source,
        string Status,
        DateTimeOffset FirstSeenUtc,
        DateTimeOffset LastSeenUtc,
        string? Notes
    );

    public async Task UpsertSeenAsync(
        int chainId,
        string token0,
        string token1,
        int feeTier,
        string source,
        string status,
        DateTimeOffset seenUtc,
        string? notes = null,
        CancellationToken ct = default)
    {
        // normalize token casing and pair ordering
        var a = (token0 ?? "").Trim().ToUpperInvariant();
        var b = (token1 ?? "").Trim().ToUpperInvariant();
        if (string.CompareOrdinal(a, b) > 0)
            (a, b) = (b, a);

        const string sql = @"
INSERT INTO active_pools
(chain_id, token0, token1, fee_tier, source, status, first_seen_utc, last_seen_utc, notes)
VALUES
(@ChainId, @Token0, @Token1, @FeeTier, @Source, @Status, @SeenUtc, @SeenUtc, @Notes)
ON CONFLICT(chain_id, token0, token1, fee_tier)
DO UPDATE SET
    source = excluded.source,
    status = excluded.status,
    last_seen_utc = excluded.last_seen_utc,
    notes = COALESCE(excluded.notes, active_pools.notes);";

        var args = new
        {
            ChainId = chainId,
            Token0 = a,
            Token1 = b,
            FeeTier = feeTier,
            Source = source,
            Status = status,
            SeenUtc = seenUtc.UtcDateTime.ToString("O"),
            Notes = notes
        };

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ActivePoolRow>> ListAsync(int take = 500, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  chain_id      AS ChainId,
  token0        AS Token0,
  token1        AS Token1,
  fee_tier      AS FeeTier,
  source        AS Source,
  status        AS Status,
  first_seen_utc AS FirstSeenUtc,
  last_seen_utc AS LastSeenUtc,
  notes         AS Notes
FROM active_pools
ORDER BY last_seen_utc DESC
LIMIT @Take;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync(sql, new { Take = Math.Clamp(take, 1, 5000) });

        var list = new List<ActivePoolRow>();
        foreach (var r in rows)
        {
            list.Add(new ActivePoolRow(
                ChainId: (int)r.ChainId,
                Token0: (string)r.Token0,
                Token1: (string)r.Token1,
                FeeTier: (int)r.FeeTier,
                Source: (string)r.Source,
                Status: (string)r.Status,
                FirstSeenUtc: DateTimeOffset.Parse((string)r.FirstSeenUtc, null, DateTimeStyles.RoundtripKind),
                LastSeenUtc: DateTimeOffset.Parse((string)r.LastSeenUtc, null, DateTimeStyles.RoundtripKind),
                Notes: (string?)r.Notes
            ));
        }

        return list;
    }
}
