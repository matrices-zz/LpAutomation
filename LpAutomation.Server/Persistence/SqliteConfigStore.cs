using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using LpAutomation.Core.Models;

namespace LpAutomation.Server.Persistence;

public sealed class SqliteConfigStore : IConfigStore
{
    private readonly string _connectionString;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public SqliteConfigStore(string dbPath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        Console.WriteLine($"[INIT] SqliteConfigStore using: {dbPath}");
        Console.WriteLine($"[CONFIG STORE] Using DB: {dbPath}");
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task<StrategyConfigDocument> GetCurrentAsync()
    {
        Console.WriteLine($"[LOAD] Fetching from: {_connectionString}");
        const string sql = @"
            SELECT v.config_json
            FROM strategy_config_current c
            LEFT JOIN strategy_config_versions v ON v.id = c.version_id
            WHERE c.singleton_id = 1
            LIMIT 1;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var json = await conn.ExecuteScalarAsync<string?>(sql);

        if (string.IsNullOrWhiteSpace(json))
            return new StrategyConfigDocument();

        return JsonSerializer.Deserialize<StrategyConfigDocument>(json, JsonOptions)
               ?? new StrategyConfigDocument();
    }

    public async Task<StrategyConfigDocument> SaveNewVersionAsync(StrategyConfigDocument doc, string actor)
    {
        Console.WriteLine($"[SAVE] Writing to: {_connectionString}");
        var now = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var hash = ComputeSha256Hex(json);

        // Lineage ID for the config "document". If StrategyConfigDocument already has an ID,
        // we can reuse it later; for now generate one per save.
        var configId = Guid.NewGuid();

        const string insertVersionSql = @"
            INSERT INTO strategy_config_versions (config_id, created_utc, created_by, config_json, config_hash)
            VALUES (@ConfigId, @CreatedUtc, @CreatedBy, @ConfigJson, @ConfigHash);
            SELECT last_insert_rowid();";

        const string updateCurrentSql = @"
            INSERT INTO strategy_config_current (singleton_id, version_id, updated_utc)
            VALUES (1, @VersionId, @UpdatedUtc)
            ON CONFLICT(singleton_id) DO UPDATE SET
                version_id = excluded.version_id,
                updated_utc = excluded.updated_utc;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        var versionId = await conn.ExecuteScalarAsync<long>(
            insertVersionSql,
            new
            {
                ConfigId = configId.ToString(),
                CreatedUtc = now.ToString("O"),
                CreatedBy = actor,
                ConfigJson = json,
                ConfigHash = hash
            },
            tx);

        await conn.ExecuteAsync(
            updateCurrentSql,
            new { VersionId = versionId, UpdatedUtc = now.ToString("O") },
            tx);

        await tx.CommitAsync();

        return doc;
    }

    public async Task<List<ConfigVersionEntity>> ListVersionsAsync(int take)
    {
        const string sql = @"
SELECT
  id,
  config_id,
  created_utc,
  created_by,
  config_json,
  config_hash
FROM strategy_config_versions
ORDER BY id DESC
LIMIT @Take;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var rows = await conn.QueryAsync(sql, new { Take = take });

        var list = new List<ConfigVersionEntity>();
        foreach (var r in rows)
        {
            list.Add(new ConfigVersionEntity
            {
                Id = (long)r.id,
                ConfigId = Guid.Parse((string)r.config_id),
                CreatedUtc = DateTimeOffset.Parse((string)r.created_utc),
                CreatedBy = (string)r.created_by,
                ConfigJson = (string)r.config_json,
                ConfigHash = (string)r.config_hash
            });
        }

        return list;
    }

    public async Task<StrategyConfigDocument?> GetVersionAsync(long id)
    {
        const string sql = @"
SELECT config_json
FROM strategy_config_versions
WHERE id = @Id
LIMIT 1;";

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        var json = await conn.ExecuteScalarAsync<string?>(sql, new { Id = id });
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<StrategyConfigDocument>(json, JsonOptions);
    }

    private static string ComputeSha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}