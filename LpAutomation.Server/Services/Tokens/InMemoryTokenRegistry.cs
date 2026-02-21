using System.Collections.Concurrent;

namespace LpAutomation.Server.Services.Tokens;

public sealed class InMemoryTokenRegistry : ITokenRegistry
{
    // MVP: hardcoded map. Later: load from config or a token list service.
    // Key: (chainId, symbolUpper)
    private readonly ConcurrentDictionary<(int, string), string> _map = new();

    public InMemoryTokenRegistry()
    {
        // ===== Ethereum mainnet (1) =====
        Add(1, "WETH", "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2");
        Add(1, "USDC", "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48");
        Add(1, "USDT", "0xdAC17F958D2ee523a2206206994597C13D831ec7");

        // Wrapped BTC (Ethereum mainnet)
        Add(1, "WBTC", "0x2260FAC5E5542a773Aa44fBCfeDf7C193bc2C599");

        // TODO (when you’re ready): add Arbitrum (42161), Base (8453), BSC (56) addresses here.
        // I didn’t hardcode those yet to avoid giving you a wrong address and wasting time.
    }

    public string NormalizeForV3(int chainId, string symbol)
    {
        var s = (symbol ?? "").Trim().ToUpperInvariant();

        // Uniswap v3 pools use wrapped native, not ETH/BNB
        if (s == "ETH") return "WETH";
        if (s == "BNB") return "WBNB";

        return s;
    }

    public string ResolveAddressOrThrow(int chainId, string symbol)
    {
        var s = (symbol ?? "").Trim().ToUpperInvariant();
        if (_map.TryGetValue((chainId, s), out var addr)) return addr;

        throw new InvalidOperationException($"Token not registered: chainId={chainId}, symbol={s}");
    }

    private void Add(int chainId, string symbolUpper, string address)
        => _map[(chainId, symbolUpper)] = address;
}