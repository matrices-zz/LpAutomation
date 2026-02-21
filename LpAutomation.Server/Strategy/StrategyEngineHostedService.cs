using LpAutomation.Core.Models;
using LpAutomation.Core.Strategy;
using LpAutomation.Server.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;

using DbPoolSnapshot = LpAutomation.Server.Storage.PoolSnapshot;

namespace LpAutomation.Server.Strategy;

public sealed class StrategyEngineHostedService : BackgroundService
{
    private readonly ILogger<StrategyEngineHostedService> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMarketDataProvider _data;
    private readonly IRecommendationStore _recs;
    private readonly SnapshotRepository _snapshots;

    // ===== Regime hysteresis tuning =====
    private static readonly TimeSpan MinRegimeDwell = TimeSpan.FromMinutes(2);

    private sealed class RegimeState
    {
        public MarketRegime Current { get; set; } = MarketRegime.Sideways;
        public DateTimeOffset LastChangeUtc { get; set; } = DateTimeOffset.MinValue;

        public MarketRegime? Candidate { get; set; }
        public int CandidateCount { get; set; } = 0;
    }

    // ===== Policy knobs (TODO: move into StrategyConfigDocument + server HTTP UI) =====
    private const int HeatCoolThreshold = 40;
    private const int HeatHotThreshold = 70;

    // Hysteresis sensitivity by heat (higher heat = harder to switch regimes)
    private const int ConfirmationsCool = 2;
    private const int ConfirmationsMid = 3;
    private const int ConfirmationsHot = 4;

    // Defensive score scaling (higher heat = discourage reinvest, encourage reallocate)
    private const double ReinvestCoolBoost = 1.15;
    private const double ReinvestHotPenalty = 0.80;
    private const double ReallocateHotBoost = 1.20;

    // ===== Storage retention + rollup policy (TODO: move into StrategyConfigDocument + server HTTP UI) =====
    private static readonly TimeSpan RawRetention = TimeSpan.FromHours(72);
    private static readonly TimeSpan Bars5mRetention = TimeSpan.FromDays(180);

    // How far back to rebuild rollups each loop (upsert makes safe)
    private static readonly TimeSpan RollupLookback = TimeSpan.FromMinutes(20);

    private readonly ConcurrentDictionary<string, RegimeState> _regimeStates = new();

    public StrategyEngineHostedService(
        ILogger<StrategyEngineHostedService> log,
        IServiceScopeFactory scopeFactory,
        IMarketDataProvider data,
        IRecommendationStore recs,
        SnapshotRepository snapshots)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _data = data;
        _recs = recs;
        _snapshots = snapshots;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Strategy engine started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configStore = scope.ServiceProvider.GetRequiredService<Persistence.IConfigStore>();
                _ = await configStore.GetCurrentAsync(); // future: drive pools + knobs

                // TODO: replace with cfg-driven pool list once UI/config is ready
                var pools = new List<PoolKey>
                {
                    new PoolKey
                    {
                        ChainId = 1,
                        Token0 = "ETH",
                        Token1 = "USDC",
                        FeeTier = 3000
                    },
                    new PoolKey
                    {
                        ChainId = 1,
                        Token0 = "WBTC",
                        Token1 = "USDC",
                        FeeTier = 3000
                    }
                };

                foreach (var key in pools)
                {
                    var snap = await _data.GetSnapshotAsync(key, stoppingToken);

                    // Stable synthetic pool identifier for now (swap to real pool address later)
                    var poolId = $"{key.Token0}/{key.Token1}/{key.FeeTier}";
                    var nowUtc = DateTime.UtcNow;

                    // Persist raw snapshot
                    await _snapshots.InsertSnapshotAsync(
                        new DbPoolSnapshot(
                            ChainId: (int)key.ChainId,
                            PoolAddress: poolId,
                            TimestampUtc: snap.AsOfUtc.UtcDateTime,
                            BlockNumber: 0,
                            Price: snap.Price,
                            Liquidity: null,
                            VolumeToken0: null,
                            VolumeToken1: null
                        ),
                        stoppingToken);

                    _log.LogInformation("Saved snapshot {PoolId} price={Price}", poolId, snap.Price);

                    // Retention (raw + bars)
                    await _snapshots.PurgeOldSnapshotsAsync(nowUtc - RawRetention, stoppingToken);
                    await _snapshots.PurgeOldBars5mAsync(nowUtc - Bars5mRetention, stoppingToken);

                    // Rollup 5m bars from last N minutes of raw
                    var rollupFromUtc = nowUtc - RollupLookback;

                    var rollupSnaps = await _snapshots.GetSnapshotsAsync(
                        chainId: (int)key.ChainId,
                        poolAddress: poolId,
                        fromUtc: rollupFromUtc,
                        toUtc: nowUtc,
                        ct: stoppingToken);

                    foreach (var bar in BuildBars5m((int)key.ChainId, poolId, rollupSnaps))
                        await _snapshots.UpsertBar5mAsync(bar, stoppingToken);

                    // ========= Multi-timeframe heat =========
                    // Short horizons use raw snapshots (fast + responsive)
                    var snaps5m = await _snapshots.GetSnapshotsAsync((int)key.ChainId, poolId, nowUtc.AddMinutes(-5), nowUtc, stoppingToken);
                    var snaps15m = await _snapshots.GetSnapshotsAsync((int)key.ChainId, poolId, nowUtc.AddMinutes(-15), nowUtc, stoppingToken);
                    var snaps1h = await _snapshots.GetSnapshotsAsync((int)key.ChainId, poolId, nowUtc.AddHours(-1), nowUtc, stoppingToken);

                    // Longer horizons use 5m bars (scalable)
                    var bars6h = await _snapshots.GetBars5mAsync((int)key.ChainId, poolId, nowUtc.AddHours(-6), nowUtc, stoppingToken);
                    var bars24h = await _snapshots.GetBars5mAsync((int)key.ChainId, poolId, nowUtc.AddHours(-24), nowUtc, stoppingToken);
                    var bars30d = await _snapshots.GetBars5mAsync((int)key.ChainId, poolId, nowUtc.AddDays(-30), nowUtc, stoppingToken);

                    var vol5m = ComputeRealizedVol(snaps5m);
                    var vol15m = ComputeRealizedVol(snaps15m);
                    var vol1h = ComputeRealizedVol(snaps1h);

                    var vol6h = ComputeRealizedVol(bars6h);
                    var vol24h = ComputeRealizedVol(bars24h);
                    var vol30d = ComputeRealizedVol(bars30d);

                    var ret5m = ComputeLogReturn(snaps5m);
                    var ret15m = ComputeLogReturn(snaps15m);
                    var ret1h = ComputeLogReturn(snaps1h);

                    var ret6h = ComputeLogReturn(bars6h);
                    var ret24h = ComputeLogReturn(bars24h);
                    var ret30d = ComputeLogReturn(bars30d);

                    // Heat “pairs”
                    int? tactical = ComputeHeatFromPair(vol5m, vol1h, ret5m, ret1h);     // now vs recent baseline
                    int? structural = ComputeHeatFromPair(vol15m, vol6h, ret15m, ret6h); // intraday structure
                    int? macro = ComputeHeatFromPair(vol1h, vol24h, ret1h, ret24h);      // day context
                    int? superMacro = ComputeHeatFromPair(vol24h, vol30d, ret24h, ret30d); // month context

                    var blendedHeat = BlendHeat(tactical, structural, superMacro);

                    _log.LogInformation(
                        "HEAT {PoolId}: blended={Blend}/100 (T={T} S={S} M={M} SM={SM})",
                        poolId,
                        blendedHeat,
                        tactical ?? -1,
                        structural ?? -1,
                        macro ?? -1,
                        superMacro ?? -1);

                    // ========= Regime detection + heat-aware hysteresis =========
                    var detected = RegimeDetector.Detect(
                        snap,
                        sidewaysVolMax: 0.06,
                        sidewaysR2Max: 0.35,
                        trendR2Min: 0.70,
                        trendSlopeAbsMin: 0.005,
                        volatileVolMin: 0.12,
                        volatileR2Max: 0.55);

                    var confirmationsRequired =
                        blendedHeat >= HeatHotThreshold ? ConfirmationsHot :
                        blendedHeat <= HeatCoolThreshold ? ConfirmationsCool :
                        ConfirmationsMid;

                    var state = _regimeStates.GetOrAdd(
                        poolId,
                        _ => new RegimeState { Current = detected, LastChangeUtc = DateTimeOffset.UtcNow });

                    var now = DateTimeOffset.UtcNow;

                    if (detected == state.Current)
                    {
                        state.Candidate = null;
                        state.CandidateCount = 0;
                    }
                    else
                    {
                        var dwellOk = (now - state.LastChangeUtc) >= MinRegimeDwell;

                        if (!dwellOk)
                        {
                            state.Candidate = detected;
                            state.CandidateCount = 0;
                        }
                        else
                        {
                            if (state.Candidate is null || state.Candidate.Value != detected)
                            {
                                state.Candidate = detected;
                                state.CandidateCount = 1;
                            }
                            else
                            {
                                state.CandidateCount++;
                            }

                            if (state.CandidateCount >= confirmationsRequired)
                            {
                                _log.LogInformation(
                                    "REGIME SWITCH {PoolId}: {Old} -> {New} (confirmed {N}x, req {Req}, heat {Heat})",
                                    poolId, state.Current, detected, state.CandidateCount, confirmationsRequired, blendedHeat);

                                state.Current = detected;
                                state.LastChangeUtc = now;
                                state.Candidate = null;
                                state.CandidateCount = 0;
                            }
                        }
                    }

                    var regime = state.Current;

                    // ========= Scoring + defensive policy =========
                    var reinvest = Scoring.ScoreReinvest(snap, regime);
                    var realloc = Scoring.ScoreReallocate(snap, regime);

                    double reinvestAdj = reinvest;
                    double reallocAdj = realloc;

                    if (blendedHeat <= HeatCoolThreshold)
                    {
                        reinvestAdj *= ReinvestCoolBoost;
                    }
                    else if (blendedHeat >= HeatHotThreshold)
                    {
                        reinvestAdj *= ReinvestHotPenalty;
                        reallocAdj *= ReallocateHotBoost;

                        // If longer horizon is also hot, go more defensive
                        if ((superMacro ?? 0) >= HeatHotThreshold)
                            reinvestAdj *= 0.90;
                    }

                    reinvestAdj = Clamp0To100(reinvestAdj);
                    reallocAdj = Clamp0To100(reallocAdj);

                    var reinvestFinal = (int)Math.Round(reinvestAdj, MidpointRounding.AwayFromZero);
                    var reallocFinal = (int)Math.Round(reallocAdj, MidpointRounding.AwayFromZero);

                    // Emit heat breakdown to DetailsJson for the desktop
                    var details = JsonSerializer.Serialize(new
                    {
                        heat = new
                        {
                            blended = blendedHeat,
                            tactical,
                            structural,
                            macro,
                            superMacro
                        },
                        ret = new
                        {
                            m5 = ret5m,
                            m15 = ret15m,
                            h1 = ret1h,
                            h6 = ret6h,
                            h24 = ret24h,
                            d30 = ret30d
                        },
                        vol = new
                        {
                            m5 = vol5m,
                            m15 = vol15m,
                            h1 = vol1h,
                            h6 = vol6h,
                            h24 = vol24h,
                            d30 = vol30d
                        }
                    });

                    var rec = new Recommendation(
                        Id: Guid.NewGuid(),
                        CreatedUtc: DateTimeOffset.UtcNow,
                        ChainId: (int)key.ChainId,
                        Token0: key.Token0 ?? "",
                        Token1: key.Token1 ?? "",
                        FeeTier: key.FeeTier,
                        Regime: regime,
                        ReinvestScore: reinvestFinal,
                        ReallocateScore: reallocFinal,
                        Summary: $"Regime={regime}, Heat={blendedHeat} (T={tactical},S={structural},M={macro},SM={superMacro}), Reinvest={reinvestAdj:F1}, Reallocate={reallocAdj:F1}",
                        DetailsJson: details
                    );

                    _recs.Add(rec);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Strategy engine loop failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _log.LogInformation("Strategy engine stopped.");
    }

    private static double Clamp0To100(double v)
    {
        if (v < 0) return 0;
        if (v > 100) return 100;
        return v;
    }

    private static int BlendHeat(int? tactical, int? structural, int? superMacro)
    {
        var parts = new List<(double w, int v)>();

        if (tactical is not null) parts.Add((0.50, tactical.Value));
        if (structural is not null) parts.Add((0.30, structural.Value));
        if (superMacro is not null) parts.Add((0.20, superMacro.Value));

        if (parts.Count == 0) return 50;

        var wsum = parts.Sum(p => p.w);
        var score = parts.Sum(p => p.w * p.v) / wsum;

        if (score < 0) score = 0;
        if (score > 100) score = 100;

        return (int)Math.Round(score, MidpointRounding.AwayFromZero);
    }

    private static int? ComputeHeatFromPair(double? volShort, double? volLong, double? retShort, double? retLong)
    {
        double? volRatio = null;
        if (volShort is not null && volLong is not null && volLong.Value > 0)
            volRatio = volShort.Value / volLong.Value;

        if (volRatio is null && retShort is null && retLong is null)
            return null;

        return ComputeMarketHeatScore(volRatio, retShort, retLong);
    }

    private static double? ComputeRealizedVol(IReadOnlyList<DbPoolSnapshot> snaps)
    {
        if (snaps.Count < 3) return null;

        var returns = new List<double>(snaps.Count - 1);
        for (int i = 1; i < snaps.Count; i++)
        {
            var p0 = snaps[i - 1].Price;
            var p1 = snaps[i].Price;
            if (p0 <= 0 || p1 <= 0) continue;
            returns.Add(Math.Log(p1 / p0));
        }

        if (returns.Count < 2) return null;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / returns.Count;
        return Math.Sqrt(variance);
    }

    private static double? ComputeRealizedVol(IReadOnlyList<PriceBar> bars)
    {
        if (bars.Count < 3) return null;

        var returns = new List<double>(bars.Count - 1);
        for (int i = 1; i < bars.Count; i++)
        {
            var p0 = bars[i - 1].Close;
            var p1 = bars[i].Close;
            if (p0 <= 0 || p1 <= 0) continue;
            returns.Add(Math.Log(p1 / p0));
        }

        if (returns.Count < 2) return null;

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / returns.Count;
        return Math.Sqrt(variance);
    }

    private static double? ComputeLogReturn(IReadOnlyList<DbPoolSnapshot> snaps)
    {
        if (snaps.Count < 2) return null;

        var p0 = snaps.First().Price;
        var p1 = snaps.Last().Price;
        if (p0 <= 0 || p1 <= 0) return null;

        return Math.Log(p1 / p0);
    }

    private static double? ComputeLogReturn(IReadOnlyList<PriceBar> bars)
    {
        if (bars.Count < 2) return null;

        var p0 = bars.First().Open;
        var p1 = bars.Last().Close;
        if (p0 <= 0 || p1 <= 0) return null;

        return Math.Log(p1 / p0);
    }

    private static DateTime TruncateTo5Minute(DateTime utc)
    {
        var m = (utc.Minute / 5) * 5;
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, m, 0, DateTimeKind.Utc);
    }

    private static IEnumerable<PriceBar> BuildBars5m(int chainId, string poolId, IReadOnlyList<DbPoolSnapshot> snaps)
    {
        var groups = snaps
            .GroupBy(s => TruncateTo5Minute(s.TimestampUtc.ToUniversalTime()))
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            var ordered = g.OrderBy(x => x.TimestampUtc).ToList();
            var open = ordered.First().Price;
            var close = ordered.Last().Price;
            var high = ordered.Max(x => x.Price);
            var low = ordered.Min(x => x.Price);

            yield return new PriceBar(
                ChainId: chainId,
                PoolAddress: poolId,
                TimestampUtc: g.Key,
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Samples: ordered.Count
            );
        }
    }

    private static int ComputeMarketHeatScore(double? volRatio, double? retShort, double? retLong)
    {
        if (volRatio is null && retShort is null && retLong is null) return 50;

        double score = 50;

        if (volRatio is not null)
        {
            // >1 => hotter, <1 => calmer
            score += (volRatio.Value - 1.0) * 30.0;
        }

        if (retShort is not null && retLong is not null)
        {
            // disagreement = chop/instability
            var disagree = Math.Sign(retShort.Value) != Math.Sign(retLong.Value);
            if (disagree) score += 10;

            // large short-term moves add heat
            score += Math.Min(10, Math.Abs(retShort.Value) * 200);
        }
        else if (retShort is not null)
        {
            score += Math.Min(10, Math.Abs(retShort.Value) * 200);
        }

        if (score < 0) score = 0;
        if (score > 100) score = 100;

        return (int)Math.Round(score, MidpointRounding.AwayFromZero);
    }
}