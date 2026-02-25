using System.Globalization;
using Dapper;
using LpAutomation.Contracts.PaperPositions;
using Microsoft.Data.Sqlite;

namespace LpAutomation.Server.PaperPositions;

public sealed class SqlitePaperPositionStore : IPaperPositionStore
{
    private readonly string _connectionString;

    public SqlitePaperPositionStore(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task<IReadOnlyList<PaperPositionDto>> ListAsync(string? ownerTag = null, int take = 200, CancellationToken ct = default)
    {
        const string sqlBase = @"
SELECT
  position_id            AS PositionId,
  owner_tag              AS OwnerTag,
  chain_id               AS ChainId,
  dex                    AS Dex,
  pool_address           AS PoolAddress,
  token0_symbol          AS Token0Symbol,
  token1_symbol          AS Token1Symbol,
  fee_tier               AS FeeTier,
  liquidity_notional_usd AS LiquidityNotionalUsd,
  entry_price            AS EntryPrice,
  tick_lower             AS TickLower,
  tick_upper             AS TickUpper,
  opened_utc             AS OpenedUtc,
  updated_utc            AS UpdatedUtc,
  enabled                AS Enabled,
  notes                  AS Notes
FROM paper_positions
/**where**/
ORDER BY updated_utc DESC
LIMIT @Take;";

        var sql = sqlBase;
        var p = new DynamicParameters();
        p.Add("Take", Math.Clamp(take, 1, 2000));

        if (!string.IsNullOrWhiteSpace(ownerTag))
        {
            sql = sql.Replace("/**where**/", "WHERE owner_tag = @OwnerTag");
            p.Add("OwnerTag", ownerTag.Trim());
        }
        else
        {
            sql = sql.Replace("/**where**/", "");
        }

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var rows = await conn.QueryAsync(sql, p);
        return rows.Select(Map).ToList();
    }

    public async Task<PaperPositionDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT
  position_id            AS PositionId,
  owner_tag              AS OwnerTag,
  chain_id               AS ChainId,
  dex                    AS Dex,
  pool_address           AS PoolAddress,
  token0_symbol          AS Token0Symbol,
  token1_symbol          AS Token1Symbol,
  fee_tier               AS FeeTier,
  liquidity_notional_usd AS LiquidityNotionalUsd,
  entry_price            AS EntryPrice,
  tick_lower             AS TickLower,
  tick_upper             AS TickUpper,
  opened_utc             AS OpenedUtc,
  updated_utc            AS UpdatedUtc,
  enabled                AS Enabled,
  notes                  AS Notes
FROM paper_positions
WHERE position_id = @Id
LIMIT 1;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var row = await conn.QueryFirstOrDefaultAsync(sql, new { Id = id.ToString() });
        return row is null ? null : Map(row);
    }

    public async Task<PaperPositionDto> UpsertAsync(Guid? id, UpsertPaperPositionRequest req, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var positionId = id ?? Guid.NewGuid();

        var ownerTag = (req.OwnerTag ?? "").Trim();
        var dex = (req.Dex ?? "").Trim();
        var poolAddress = (req.PoolAddress ?? "").Trim();

        var token0 = (req.Token0Symbol ?? "").Trim().ToUpperInvariant();
        var token1 = (req.Token1Symbol ?? "").Trim().ToUpperInvariant();
        if (string.CompareOrdinal(token0, token1) > 0)
            (token0, token1) = (token1, token0);

        const string sql = @"
INSERT INTO paper_positions
(position_id, owner_tag, chain_id, dex, pool_address, token0_symbol, token1_symbol, fee_tier,
 liquidity_notional_usd, entry_price, tick_lower, tick_upper, opened_utc, updated_utc, enabled, notes)
VALUES
(@PositionId, @OwnerTag, @ChainId, @Dex, @PoolAddress, @Token0Symbol, @Token1Symbol, @FeeTier,
 @LiquidityNotionalUsd, @EntryPrice, @TickLower, @TickUpper, @NowUtc, @NowUtc, @Enabled, @Notes)
ON CONFLICT(position_id)
DO UPDATE SET
  owner_tag = excluded.owner_tag,
  chain_id = excluded.chain_id,
  dex = excluded.dex,
  pool_address = excluded.pool_address,
  token0_symbol = excluded.token0_symbol,
  token1_symbol = excluded.token1_symbol,
  fee_tier = excluded.fee_tier,
  liquidity_notional_usd = excluded.liquidity_notional_usd,
  entry_price = excluded.entry_price,
  tick_lower = excluded.tick_lower,
  tick_upper = excluded.tick_upper,
  updated_utc = excluded.updated_utc,
  enabled = excluded.enabled,
  notes = excluded.notes;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PositionId = positionId.ToString(),
            OwnerTag = ownerTag,
            ChainId = req.ChainId,
            Dex = dex,
            PoolAddress = poolAddress,
            Token0Symbol = token0,
            Token1Symbol = token1,
            FeeTier = req.FeeTier,
            LiquidityNotionalUsd = req.LiquidityNotionalUsd,
            EntryPrice = req.EntryPrice,
            TickLower = req.TickLower,
            TickUpper = req.TickUpper,
            NowUtc = now.ToString("O"),
            Enabled = req.Enabled ? 1 : 0,
            Notes = req.Notes
        }, cancellationToken: ct));

        // return canonical persisted row
        return await GetAsync(positionId, ct)
               ?? throw new InvalidOperationException("Upsert succeeded but row not found.");
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"DELETE FROM paper_positions WHERE position_id = @Id;";

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<PaperPositionDto?> FindBestMatchAsync(
        string? ownerTag,
        int chainId,
        string token0Symbol,
        string token1Symbol,
        int feeTier,
        CancellationToken ct = default)
    {
        var a = (token0Symbol ?? "").Trim().ToUpperInvariant();
        var b = (token1Symbol ?? "").Trim().ToUpperInvariant();
        if (string.CompareOrdinal(a, b) > 0)
            (a, b) = (b, a);

        var sql = @"
SELECT
  position_id            AS PositionId,
  owner_tag              AS OwnerTag,
  chain_id               AS ChainId,
  dex                    AS Dex,
  pool_address           AS PoolAddress,
  token0_symbol          AS Token0Symbol,
  token1_symbol          AS Token1Symbol,
  fee_tier               AS FeeTier,
  liquidity_notional_usd AS LiquidityNotionalUsd,
  entry_price            AS EntryPrice,
  tick_lower             AS TickLower,
  tick_upper             AS TickUpper,
  opened_utc             AS OpenedUtc,
  updated_utc            AS UpdatedUtc,
  enabled                AS Enabled,
  notes                  AS Notes
FROM paper_positions
WHERE enabled = 1
  AND chain_id = @ChainId
  AND token0_symbol = @Token0
  AND token1_symbol = @Token1
  AND fee_tier = @FeeTier
  /**owner**/
ORDER BY updated_utc DESC
LIMIT 1;";

        var p = new DynamicParameters(new
        {
            ChainId = chainId,
            Token0 = a,
            Token1 = b,
            FeeTier = feeTier
        });

        if (!string.IsNullOrWhiteSpace(ownerTag))
        {
            sql = sql.Replace("/**owner**/", "AND owner_tag = @OwnerTag");
            p.Add("OwnerTag", ownerTag.Trim());
        }
        else
        {
            sql = sql.Replace("/**owner**/", "");
        }

        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);

        var row = await conn.QueryFirstOrDefaultAsync(sql, p);
        return row is null ? null : Map(row);
    }

    private static PaperPositionDto Map(dynamic r)
    {
        return new PaperPositionDto(
            PositionId: Guid.Parse((string)r.PositionId),
            OwnerTag: (string)r.OwnerTag,
            ChainId: (int)r.ChainId,
            Dex: (string)r.Dex,
            PoolAddress: (string)r.PoolAddress,
            Token0Symbol: (string)r.Token0Symbol,
            Token1Symbol: (string)r.Token1Symbol,
            FeeTier: (int)r.FeeTier,
            LiquidityNotionalUsd: Convert.ToDecimal(r.LiquidityNotionalUsd, CultureInfo.InvariantCulture),
            EntryPrice: Convert.ToDecimal(r.EntryPrice, CultureInfo.InvariantCulture),
            TickLower: (int)r.TickLower,
            TickUpper: (int)r.TickUpper,
            OpenedUtc: DateTimeOffset.Parse((string)r.OpenedUtc, null, DateTimeStyles.RoundtripKind),
            UpdatedUtc: DateTimeOffset.Parse((string)r.UpdatedUtc, null, DateTimeStyles.RoundtripKind),
            Enabled: Convert.ToInt32(r.Enabled, CultureInfo.InvariantCulture) == 1,
            Notes: (string?)r.Notes
        );
    }
}
