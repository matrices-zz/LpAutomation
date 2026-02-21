using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LpAutomation.Server.Services.Pools;

public sealed class JsonRpcUniswapV3FactoryClient : IOnChainPoolFactoryClient
{
    private readonly HttpClient _http;

    // MVP: config-driven (recommended)
    private readonly Dictionary<(int chainId, string dex), string> _rpcUrl = new();
    private readonly Dictionary<(int chainId, string dex), string> _factory = new();

    private readonly RpcProviderOptions _opt;

    public JsonRpcUniswapV3FactoryClient(HttpClient http, IOptions<RpcProviderOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
        _factory[(1, "UniswapV3")] = "0x1F98431c8aD98523631AE4a59f267346ea31F984";
    }

    public async Task<string?> GetPoolAsync(int chainId, string dex, string token0, string token1, int feeTier, CancellationToken ct)
    {
        if (!_opt.RpcProviders.TryGetValue(dex, out var perChain) || !perChain.TryGetValue(chainId, out var rpc))
            throw new InvalidOperationException($"No RPC configured for chainId={chainId}, dex={dex}. Check appsettings RpcProviders.");

        if (!_factory.TryGetValue((chainId, dex), out var factory))
            throw new InvalidOperationException($"No factory configured for chainId={chainId}, dex={dex}");

        // UniswapV3Factory.getPool(address,address,uint24)
        // function selector = 0x1698ee82
        var data = "0x1698ee82"
            + Pad32(token0)
            + Pad32(token1)
            + Pad32UInt(feeTier);

        var payload = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "eth_call",
            @params = new object[]
            {
                new { to = factory, data },
                "latest"
            }
        };

        using var resp = await _http.PostAsJsonAsync(rpc, payload, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        System.Diagnostics.Debug.WriteLine(json);
        var root = doc.RootElement;

        // JSON-RPC error handling
        if (root.TryGetProperty("error", out var err))
        {
            var code = err.TryGetProperty("code", out var c) ? c.ToString() : "(no code)";
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "(no message)";
            var errorData = err.TryGetProperty("data", out var d) ? d.ToString() : null;

            throw new InvalidOperationException($"RPC error code={code}, message={msg}, data={errorData}");
        }

        if (!root.TryGetProperty("result", out var resultEl))
        {
            throw new InvalidOperationException($"RPC response missing 'result'. Full response: {json}");
        }

        var result = resultEl.GetString();
        if (string.IsNullOrWhiteSpace(result) || result.Length < 66) return null;


        // result is 32-byte address padded. Take last 40 hex chars
        var addr = "0x" + result[^40..];

        System.Diagnostics.Debug.WriteLine($"getPool({token0},{token1},{feeTier}) => {addr}");

        return addr;
    }

    private static string Pad32(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is null/empty.");

        var a = address.Trim();
        if (a.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            a = a[2..];

        // Must be exactly 20 bytes = 40 hex chars
        if (a.Length != 40)
            throw new ArgumentException($"Address must be 40 hex chars (20 bytes). Got length={a.Length}: {address}");

        // Left pad to 32 bytes
        return a.PadLeft(64, '0');
    }


    private static string Pad32UInt(int v)
    {
        if (v < 0) throw new ArgumentOutOfRangeException(nameof(v));
        return v.ToString("x").PadLeft(64, '0');
    }
}
