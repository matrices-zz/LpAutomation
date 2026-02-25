using LpAutomation.Core.Strategy;
using LpAutomation.Server.PaperPositions;
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
    private readonly IPaperPositionStore _paper;

    public RecommendationsController(
        IRecommendationStore store,
        ITokenRegistry tokens,
        IPoolAddressResolver pools,
        IPaperPositionStore paper)
    {
        _store = store;
        _tokens = tokens;
        _pools = pools;
        _paper = paper;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int take = 50,
        [FromQuery] string? ownerTag = null,
        CancellationToken ct = default)
    {
        var list = _store.GetLatest(Math.Clamp(take, 1, 200));
        var outList = new List<object>(list.Count);

        foreach (var r in list)
        {
            var chainId = (int)r.ChainId;
            var dex = "UniswapV3";
            var protocolVersion = "v3";

            var sym0 = _tokens.NormalizeForV3(chainId, r.Token0);
            var sym1 = _tokens.NormalizeForV3(chainId, r.Token1);

            string? token0Addr = null;
            string? token1Addr = null;
            string? poolAddr = null;

            var detailsJson = r.DetailsJson;

            try
            {
                token0Addr = _tokens.ResolveAddressOrThrow(chainId, sym0);
                token1Addr = _tokens.ResolveAddressOrThrow(chainId, sym1);
                poolAddr = await _pools.ResolveV3PoolAddressAsync(chainId, dex, token0Addr, token1Addr, r.FeeTier, ct);
            }
            catch (Exception ex)
            {
                var diag = $"PoolResolveError: {ex.GetType().Name}: {ex.Message}";
                detailsJson = string.IsNullOrWhiteSpace(detailsJson) ? diag : $"{detailsJson}\n{diag}";
                poolAddr = null;
            }

            var paperPosition = await _paper.FindBestMatchAsync(
                ownerTag: ownerTag,
                chainId: chainId,
                token0Symbol: r.Token0,
                token1Symbol: r.Token1,
                feeTier: r.FeeTier,
                ct: ct);

            outList.Add(new
            {
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

                dex,
                protocolVersion,
                token0Address = token0Addr,
                token1Address = token1Addr,
                poolAddress = poolAddr,

                paperPosition
            });
        }

        return Ok(outList);
    }
}
