using LpAutomation.Server.Analytics;
using LpAutomation.Server.Storage;
using Microsoft.AspNetCore.Mvc;

namespace LpAutomation.Server.Controllers;

[ApiController]
[Route("api/returns")]
public sealed class ReturnsFrameController : ControllerBase
{
    private readonly SnapshotRepository _repo;
    private readonly ReturnsFrameBuilder _builder;

    public ReturnsFrameController(SnapshotRepository repo)
    {
        _repo = repo;
        _builder = new ReturnsFrameBuilder(repo);
    }

    [HttpGet("frame")]
    public async Task<IActionResult> GetFrame(
        [FromQuery] int chainId,
        [FromQuery] string interval = "m5",
        [FromQuery] int lookbackHours = 24,
        [FromQuery] string? pools = null,
        [FromQuery] int takePools = 6,
        CancellationToken ct = default)
    {
        if (chainId <= 0)
            return Ok(new { ok = false, message = "chainId must be > 0" });

        if (lookbackHours <= 0)
            return Ok(new { ok = false, message = "lookbackHours must be > 0" });

        var iv = interval.Trim().ToLowerInvariant() switch
        {
            "m1" => SnapshotRepository.BarInterval.M1,
            _ => SnapshotRepository.BarInterval.M5
        };

        List<string> poolList;

        if (!string.IsNullOrWhiteSpace(pools))
        {
            poolList = pools
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => p.ToLowerInvariant())
                .ToList();
        }
        else
        {
            var known = await _repo.GetKnownPoolsAsync(chainId, ct);
            poolList = known.Take(Math.Max(2, takePools)).ToList();
        }

        if (poolList.Count < 2)
            return Ok(new { ok = false, message = "Need at least 2 pools (use pools=... or ensure DB has >=2 pools)." });

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-lookbackHours);

        try
        {
            // Intersection (strict) + diagnostics
            var frame = await _builder.BuildIntersectionFrameWithDiagnosticsAsync(
                chainId, poolList, iv, fromUtc, toUtc, ct);

            if (!frame.Ok)
            {
                return Ok(new
                {
                    ok = false,
                    message = frame.Message,
                    chainId,
                    interval = iv.ToString(),
                    fromUtc,
                    toUtc,
                    pools = poolList,
                    points = frame.Points,
                    perPoolCounts = frame.PerPoolCounts
                });
            }

            // Compact response: timestamps + map(pool -> returns[])
            var map = new Dictionary<string, double[]>();
            for (int i = 0; i < frame.Pools.Count; i++)
                map[frame.Pools[i]] = frame.Returns[i];

            return Ok(new
            {
                ok = true,
                chainId,
                interval = iv.ToString(),
                fromUtc,
                toUtc,
                points = frame.TimestampsUtc.Count,
                pools = frame.Pools,
                perPoolCounts = frame.PerPoolCounts,
                returnsByPool = map
            });
        }
        catch (Exception ex)
        {
            // Never 500 the desktop for analytics shape issues
            return Ok(new { ok = false, message = ex.Message });
        }
    }
}