using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LpAutomation.Core.Models;
using LpAutomation.Core.Serialization;
using LpAutomation.Core.Validation;
using LpAutomation.Contracts.Config;
using LpAutomation.Server.Persistence;
using LpAutomation.Server.Services;
using LpAutomation.Server.Storage;
using LpAutomation.Server.Strategy;
using Microsoft.AspNetCore.Mvc;

namespace LpAutomation.Server.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigController : ControllerBase
{
    private readonly IConfigStore _store;
    private readonly IRecommendationStore _recommendations;
    private readonly ActivePoolRepository _activePools;

    public ConfigController(
        IConfigStore store,
        IRecommendationStore recommendations,
        ActivePoolRepository activePools)
    {
        _store = store;
        _recommendations = recommendations;
        _activePools = activePools;
    }

    private string Actor => Environment.UserName;

    [HttpGet("current")]
    public async Task<ActionResult<ConfigGetResponse>> GetCurrent()
    {
        var cfg = await _store.GetCurrentAsync();
        return Ok(new ConfigGetResponse(cfg, ConfigHashing.Sha256Hash(cfg)));
    }

    [HttpPost("validate")]
    public ActionResult<ConfigValidateResponse> Validate([FromBody] ConfigPutRequest req)
    {
        var ac = StrategyConfigAutoCorrector.AutoCorrect(req.Config);
        var vr = StrategyConfigValidator.Validate(ac.Corrected);

        return Ok(new ConfigValidateResponse(vr.IsValid, vr.Issues, ac.Corrections));
    }

    [HttpPut("current")]
    public async Task<ActionResult<ConfigGetResponse>> PutCurrent([FromBody] ConfigPutRequest req)
    {
        var ac = StrategyConfigAutoCorrector.AutoCorrect(req.Config);
        var vr = StrategyConfigValidator.Validate(ac.Corrected);
        if (!vr.IsValid) return BadRequest(new { vr.Issues });

        var saved = await _store.SaveNewVersionAsync(ac.Corrected, Actor);
        return Ok(new ConfigGetResponse(saved, ConfigHashing.Sha256Hash(saved)));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var cfg = await _store.GetCurrentAsync();
        var json = JsonSerializer.Serialize(cfg, JsonStrict.Options);
        return File(Encoding.UTF8.GetBytes(json), "application/json", "strategy-config.json");
    }

    [HttpPost("import")]
    public async Task<ActionResult<ConfigGetResponse>> Import([FromBody] ConfigImportRequest req)
    {
        StrategyConfigDocument? cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<StrategyConfigDocument>(req.Json, JsonStrict.Options);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Strict JSON parse failed: {ex.Message}" });
        }

        if (cfg is null) return BadRequest(new { error = "Invalid JSON." });

        var ac = StrategyConfigAutoCorrector.AutoCorrect(cfg);
        var vr = StrategyConfigValidator.Validate(ac.Corrected);
        if (!vr.IsValid) return BadRequest(new { vr.Issues });

        var saved = await _store.SaveNewVersionAsync(ac.Corrected, Actor);
        return Ok(new ConfigGetResponse(saved, ConfigHashing.Sha256Hash(saved)));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ConfigHistoryItem[]>> History([FromQuery] int take = 50)
    {
        var versions = await _store.ListVersionsAsync(take);
        var items = versions
            .Select(v => new ConfigHistoryItem(v.Id, v.CreatedUtc, v.CreatedBy, v.ConfigHash))
            .ToArray();

        return Ok(items);
    }

    [HttpGet("version/{id:long}")]
    public async Task<ActionResult<StrategyConfigDocument>> GetVersion(long id)
    {
        var cfg = await _store.GetVersionAsync(id);
        return cfg is null ? NotFound() : Ok(cfg);
    }

    [HttpGet("active-pools")]
    public async Task<ActionResult<ActivePoolDto[]>> ActivePools()
    {
        // Preferred source: durable DB-backed active pools
        var durable = await _activePools.ListAsync(500);
        if (durable.Count > 0)
        {
            var fromDb = durable
                .Select(p => new ActivePoolDto(
                    ChainId: p.ChainId,
                    Token0: p.Token0,
                    Token1: p.Token1,
                    FeeTier: p.FeeTier,
                    LastSeenUtc: p.LastSeenUtc,
                    Source: p.Source,
                    Status: p.Status))
                .OrderBy(p => p.ChainId)
                .ThenBy(p => p.Token0)
                .ThenBy(p => p.Token1)
                .ThenBy(p => p.FeeTier)
                .ToArray();

            return Ok(fromDb);
        }

        // Fallback: current in-memory recommendation stream
        var fallback = _recommendations
            .GetLatest(500)
            .GroupBy(r => new { r.ChainId, r.Token0, r.Token1, r.FeeTier })
            .Select(g => new ActivePoolDto(
                ChainId: g.Key.ChainId,
                Token0: g.Key.Token0,
                Token1: g.Key.Token1,
                FeeTier: g.Key.FeeTier,
                LastSeenUtc: g.Max(x => x.CreatedUtc),
                Source: "recommendations-fallback",
                Status: "active"))
            .OrderBy(p => p.ChainId)
            .ThenBy(p => p.Token0)
            .ThenBy(p => p.Token1)
            .ThenBy(p => p.FeeTier)
            .ToArray();

        return Ok(fallback);
    }
}
