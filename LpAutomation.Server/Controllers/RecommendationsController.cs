using LpAutomation.Core.Strategy;
using LpAutomation.Server.Services.Pools;
using LpAutomation.Server.Services.Tokens;
using LpAutomation.Server.Strategy;
using Microsoft.AspNetCore.Mvc;

namespace LpAutomation.Server.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly IRecommendationStore _store;
    private readonly ITokenRegistry _tokens;
    private readonly IPoolAddressResolver _pools;

    public RecommendationsController(IRecommendationStore store, ITokenRegistry tokens, IPoolAddressResolver pools)
    {
        _store = store;
        _tokens = tokens;
        _pools = pools;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int take = 50, CancellationToken ct = default)
    {
        var list = _store.GetLatest(Math.Clamp(take, 1, 200));

        // IMPORTANT: keep the original fields to avoid breaking Desktop.
        var outList = new List<object>(list.Count);

        foreach (var r in list)
        {
            var chainId = (int)r.ChainId;
            var dex = "UniswapV3";
            var protocolVersion = "v3";

            // Normalize symbols for pool identity (ETH -> WETH)
            var sym0 = _tokens.NormalizeForV3(chainId, r.Token0);
            var sym1 = _tokens.NormalizeForV3(chainId, r.Token1);

            string? token0Addr = null;
            string? token1Addr = null;
            string? poolAddr = null;

            // Keep original detailsJson but append diagnostics if needed
            var detailsJson = r.DetailsJson;

            try
            {
                token0Addr = _tokens.ResolveAddressOrThrow(chainId, sym0);
                token1Addr = _tokens.ResolveAddressOrThrow(chainId, sym1);

                // Use address ordering inside resolver; it caches results
                poolAddr = await _pools.ResolveV3PoolAddressAsync(chainId, dex, token0Addr, token1Addr, r.FeeTier, ct);
            }
            catch (Exception ex)
            {
                var diag = $"PoolResolveError: {ex.GetType().Name}: {ex.Message}";

                // If detailsJson already exists, append in a safe human-readable way
                detailsJson = string.IsNullOrWhiteSpace(detailsJson)
                    ? diag
                    : $"{detailsJson}\n{diag}";

                // IMPORTANT: do not throw — return what we have so Desktop doesn't 500
                token0Addr = token0Addr ?? null;
                token1Addr = token1Addr ?? null;
                poolAddr = null;
            }

            outList.Add(new
            {
                // Original fields expected by Desktop
                id = r.Id,
                createdUtc = r.CreatedUtc,
                chainId = r.ChainId,
                token0 = r.Token0,
                token1 = r.Token1,
                feeTier = r.FeeTier,
                regime = (int)r.Regime,
                reinvestScore = r.ReinvestScore,
                reallocateScore = r.ReallocateScore,
                summary = r.Summary,
                detailsJson = detailsJson,

                // Identity fields (nice-to-have)
                dex,
                protocolVersion,
                token0Address = token0Addr,
                token1Address = token1Addr,
                poolAddress = poolAddr
            });
        }

        return Ok(outList);
    }
}