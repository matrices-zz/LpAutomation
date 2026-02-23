using Microsoft.Data.Sqlite;
using System.IO;

namespace LpAutomation.Server.Storage;

public static class SqliteDbInitializer
{
    public static string GetDefaultDbPath()
    {
        // Server runs with working directory near the executable (typical in dev + service scenarios).
        // DB will live in: ./data/lpautomation.sqlite
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "lpautomation.sqlite");
    }

    public static async Task InitializeAsync(string? dbPath = null, CancellationToken ct = default)
    {
        dbPath ??= GetDefaultDbPath();
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync(ct);

        // Helpful pragmas for durability + performance for time-series writes.
        // WAL is great for concurrent readers (desktop querying while server writes).
        await ExecuteAsync(conn, "PRAGMA journal_mode=WAL;", ct);
        await ExecuteAsync(conn, "PRAGMA synchronous=NORMAL;", ct);
        await ExecuteAsync(conn, "PRAGMA temp_store=MEMORY;", ct);

        // ===== Raw snapshots (append-only) =====
        var createSnapshots = @"
CREATE TABLE IF NOT EXISTS pool_snapshots (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    chain_id        INTEGER NOT NULL,
    pool_address    TEXT    NOT NULL,
    ts_utc          TEXT    NOT NULL,
    block_number    INTEGER NOT NULL,
    price           REAL    NOT NULL,
    liquidity       REAL    NULL,
    volume_token0   REAL    NULL,
    volume_token1   REAL    NULL
);

CREATE INDEX IF NOT EXISTS ix_pool_snapshots_pool_ts
    ON pool_snapshots (chain_id, pool_address, ts_utc);

CREATE INDEX IF NOT EXISTS ix_pool_snapshots_pool_block
    ON pool_snapshots (chain_id, pool_address, block_number);

CREATE UNIQUE INDEX IF NOT EXISTS ux_pool_snapshots_pool_block_nonzero
    ON pool_snapshots (chain_id, pool_address, block_number)
    WHERE block_number > 0;
";
        await ExecuteAsync(conn, createSnapshots, ct);
        await ExecuteAsync(conn, createSnapshots, ct);
        await EnsureColumnExistsAsync(conn, "pool_snapshots", "source", "TEXT NULL", ct);
        await EnsureColumnExistsAsync(conn, "pool_snapshots", "latency_ms", "INTEGER NULL", ct);
        await EnsureColumnExistsAsync(conn, "pool_snapshots", "finality_status", "TEXT NULL", ct);
        await EnsureColumnExistsAsync(conn, "pool_snapshots", "quality_flags", "TEXT NULL", ct);


        // ===== Bars / rollups =====
        // NOTE: SQLite supports '--' and /* */ comments, NOT '//' comments.
        var createBars = @"
CREATE TABLE IF NOT EXISTS pool_bars_1m (
  chain_id       INTEGER NOT NULL,
  pool_address   TEXT    NOT NULL,
  ts_utc         TEXT    NOT NULL,
  open           REAL    NOT NULL,
  high           REAL    NOT NULL,
  low            REAL    NOT NULL,
  close          REAL    NOT NULL,
  samples        INTEGER NOT NULL,
  PRIMARY KEY (chain_id, pool_address, ts_utc)
);

CREATE INDEX IF NOT EXISTS idx_pool_bars_1m_lookup
ON pool_bars_1m(chain_id, pool_address, ts_utc);

CREATE TABLE IF NOT EXISTS pool_bars_5m (
  chain_id       INTEGER NOT NULL,
  pool_address   TEXT    NOT NULL,
  ts_utc         TEXT    NOT NULL,  -- 5m bucket timestamp ISO8601
  open           REAL    NOT NULL,
  high           REAL    NOT NULL,
  low            REAL    NOT NULL,
  close          REAL    NOT NULL,
  samples        INTEGER NOT NULL,
  PRIMARY KEY (chain_id, pool_address, ts_utc)
);

CREATE INDEX IF NOT EXISTS idx_pool_bars_5m_lookup
ON pool_bars_5m(chain_id, pool_address, ts_utc);
";
        await ExecuteAsync(conn, createBars, ct);

        // ===== Simple KV config table (optional) =====
        var createConfig = @"
CREATE TABLE IF NOT EXISTS config_kv (
    key         TEXT PRIMARY KEY,
    value       TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
";
        await ExecuteAsync(conn, createConfig, ct);

        // ===== Strategy config versioning tables =====
        var createConfigTables = @"
CREATE TABLE IF NOT EXISTS strategy_config_versions (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    config_id     TEXT    NOT NULL,      -- GUID as text
    created_utc   TEXT    NOT NULL,      -- ISO8601 UTC
    created_by    TEXT    NOT NULL,
    config_json   TEXT    NOT NULL,
    config_hash   TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_strategy_config_versions_created
    ON strategy_config_versions (created_utc DESC);

CREATE TABLE IF NOT EXISTS strategy_config_current (
    singleton_id  INTEGER PRIMARY KEY CHECK (singleton_id = 1),
    version_id    INTEGER NOT NULL,
    updated_utc   TEXT    NOT NULL
);

INSERT OR IGNORE INTO strategy_config_current (singleton_id, version_id, updated_utc)
VALUES (1, 0, '1970-01-01T00:00:00.0000000Z');
";
        await ExecuteAsync(conn, createConfigTables, ct);
    }

    private static async Task EnsureColumnExistsAsync(SqliteConnection conn, string table, string column, string definition, CancellationToken ct)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";

        await using var reader = await check.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await ExecuteAsync(conn, $"ALTER TABLE {table} ADD COLUMN {column} {definition};", ct);
    }

    private static async Task ExecuteAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}