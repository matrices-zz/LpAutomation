using LpAutomation.Server.Storage;
using Microsoft.AspNetCore.Mvc;

namespace LpAutomation.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReturnsController : ControllerBase
{
    private readonly SnapshotRepository _repo;

    public ReturnsController(SnapshotRepository repo)
    {
        _repo = repo;
    }

    [HttpGet("log")]
    public async Task<IActionResult> GetLogReturns(
        [FromQuery] int chainId,
        [FromQuery] string poolAddress,
        [FromQuery] string interval = "m5",
        [FromQuery] int lookbackHours = 24,
        CancellationToken ct = default)
    {
        if (chainId <= 0) return BadRequest("chainId must be > 0");
        if (string.IsNullOrWhiteSpace(poolAddress)) return BadRequest("poolAddress is required");
        if (lookbackHours <= 0) return BadRequest("lookbackHours must be > 0");

        var iv = interval.Trim().ToLowerInvariant() switch
        {
            "m1" => SnapshotRepository.BarInterval.M1,
            _ => SnapshotRepository.BarInterval.M5
        };

        var toUtc = DateTime.UtcNow;
        var fromUtc = toUtc.AddHours(-lookbackHours);

        var rows = await _repo.GetLogReturnsAsync(chainId, poolAddress, iv, fromUtc, toUtc, ct);

        return Ok(new
        {
            chainId,
            poolAddress = poolAddress.ToLowerInvariant(),
            interval = iv.ToString(),
            fromUtc,
            toUtc,
            count = rows.Count,
            points = rows.Select(x => new { tsUtc = x.TimestampUtc, r = x.LogReturn })
        });
    }
}