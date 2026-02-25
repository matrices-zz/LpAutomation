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
    public ActionResult<IReadOnlyList<PaperPositionDto>> List(
        [FromQuery] string? ownerTag = null,
        [FromQuery] int take = 200)
    {
        var rows = _store.List(ownerTag, take);
        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PaperPositionDto> Get(Guid id)
    {
        var row = _store.Get(id);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public ActionResult<UpsertPaperPositionResponse> Create([FromBody] UpsertPaperPositionRequest req)
    {
        var created = _store.Upsert(null, req);
        return Ok(new UpsertPaperPositionResponse(created));
    }

    [HttpPut("{id:guid}")]
    public ActionResult<UpsertPaperPositionResponse> Update(Guid id, [FromBody] UpsertPaperPositionRequest req)
    {
        var updated = _store.Upsert(id, req);
        return Ok(new UpsertPaperPositionResponse(updated));
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        return _store.Delete(id) ? NoContent() : NotFound();
    }
}
