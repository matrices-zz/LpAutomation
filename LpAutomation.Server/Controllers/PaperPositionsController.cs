using LpAutomation.Contracts.PaperPositions;
using LpAutomation.Server.PaperPositions;
using Microsoft.AspNetCore.Mvc;

namespace LpAutomation.Server.Controllers;

[ApiController]
[Route("api/paper-positions")]
public sealed class PaperPositionsController : ControllerBase
{
    private readonly IPaperPositionStore _store;

    public PaperPositionsController(IPaperPositionStore store)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaperPositionDto>>> List(
        [FromQuery] string? ownerTag = null,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        return Ok(await _store.ListAsync(ownerTag, take, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaperPositionDto>> Get(Guid id, CancellationToken ct = default)
    {
        var row = await _store.GetAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<ActionResult<UpsertPaperPositionResponse>> Create(
        [FromBody] UpsertPaperPositionRequest req,
        CancellationToken ct = default)
    {
        var created = await _store.UpsertAsync(null, req, ct);
        return Ok(new UpsertPaperPositionResponse(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UpsertPaperPositionResponse>> Update(
        Guid id,
        [FromBody] UpsertPaperPositionRequest req,
        CancellationToken ct = default)
    {
        var updated = await _store.UpsertAsync(id, req, ct);
        return Ok(new UpsertPaperPositionResponse(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        return await _store.DeleteAsync(id, ct) ? NoContent() : NotFound();
    }
}
