using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Contracts.Config;
using LpAutomation.Core.Models;
using LpAutomation.Core.Serialization;
using LpAutomation.Desktop.Avalonia.Services;
using System;
using System.Data;
using System.Net.Http;
using System.Threading.Tasks;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly ConfigApiClient _api;
    private StrategyConfigDocument? _loaded;

    // ── Status ────────────────────────────────────────────────
    [ObservableProperty] private string _title = "Settings";
    [ObservableProperty] private string _statusMessage = "Load config to begin.";
    [ObservableProperty] private string _statusKind = "Neutral"; // Neutral | Running | Success | Error
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isDirty;


    // ── Server Connection ─────────────────────────────────────
    [ObservableProperty] private string _serverUrl = "https://localhost:7069";
    [ObservableProperty] private string _connectionStatus = "";
    [ObservableProperty] private string _connectionStatusColor = "Gray";

    // ── Regime — Trend ────────────────────────────────────────
    [ObservableProperty] private double _trendR2Min = 0.55;
    [ObservableProperty] private double _trendEmaSlopeAbsMin = 0.004;
    [ObservableProperty] private int _trendWindowHours = 6;

    // ── Regime — Sideways ─────────────────────────────────────
    [ObservableProperty] private double _sidewaysVolNormMax = 1.00;
    [ObservableProperty] private double _sidewaysR2Max = 0.45;
    [ObservableProperty] private double _sidewaysBandContainPctMin = 0.70;
    [ObservableProperty] private int _sidewaysWindowHours = 6;

    // ── Regime — Volatile ─────────────────────────────────────
    [ObservableProperty] private double _volatileVolNormMin = 1.30;
    [ObservableProperty] private double _volatileR2Max = 0.50;

    // ── Scoring — Reinvest Weights ────────────────────────────
    [ObservableProperty] private double _weightRewardFee = 0.60;
    [ObservableProperty] private double _weightRewardInRange = 0.40;
    [ObservableProperty] private double _weightRiskVol = 0.45;
    [ObservableProperty] private double _weightRiskDrift = 0.35;
    [ObservableProperty] private double _weightRiskTrendPenalty = 0.20;

    // ── Scoring — Reinvest Ranges ─────────────────────────────
    [ObservableProperty] private double _feeAprMin = 0.10;
    [ObservableProperty] private double _feeAprMax = 0.80;
    [ObservableProperty] private double _inRangePctMin = 0.40;
    [ObservableProperty] private double _inRangePctMax = 0.90;
    [ObservableProperty] private double _volNormMin = 0.80;
    [ObservableProperty] private double _volNormMax = 1.80;
    [ObservableProperty] private int _driftTicksMinMultiplier = 5;
    [ObservableProperty] private int _driftTicksMaxMultiplier = 40;
    [ObservableProperty] private int _scoreCentering = 50;

    // ── Scoring — Trend Penalty ───────────────────────────────
    [ObservableProperty] private int _trendPenaltySideways = 0;
    [ObservableProperty] private int _trendPenaltyTrending = 60;
    [ObservableProperty] private int _trendPenaltyVolatile = 80;

    // ── Decisions ─────────────────────────────────────────────
    [ObservableProperty] private int _compoundScoreMin = 70;
    [ObservableProperty] private int _reallocateScoreMax = 40;
    [ObservableProperty] private int _minHoursBetweenCompounds = 6;
    [ObservableProperty] private int _staleAfterSeconds = 3600;

    // ── Guardrails ────────────────────────────────────────────
    [ObservableProperty] private int _maxSlippageBps = 50;
    [ObservableProperty] private decimal _maxNotionalUsdPerProposal = 2500m;
    [ObservableProperty] private int _maxGasFeeGwei = 40;
    [ObservableProperty] private int _deadlineSecondsAtExecution = 600;
    [ObservableProperty] private bool _allowApprovals = true;
    [ObservableProperty] private decimal _maxApprovalUsd = 5000m;

    // ── Hedging ───────────────────────────────────────────────
    [ObservableProperty] private bool _hedgingEnabled = true;
    [ObservableProperty] private double _hedgingExposurePctTrigger = 0.35;
    [ObservableProperty] private double _hedgeTrendDownR2Min = 0.60;
    [ObservableProperty] private double _hedgeTrendDownEmaSlopeMax = -0.004;
    [ObservableProperty] private double _hedgeVolSpikeRatioMin = 1.80;
    [ObservableProperty] private double _hedgeSuggestTrendDownMin = 0.25;
    [ObservableProperty] private double _hedgeSuggestTrendDownMax = 0.50;
    [ObservableProperty] private double _hedgeSuggestVolSpikeMin = 0.10;
    [ObservableProperty] private double _hedgeSuggestVolSpikeMax = 0.25;

    // ─────────────────────────────────────────────────────────
    public SettingsPageViewModel() : this(null!) { }

    public SettingsPageViewModel(ConfigApiClient api)
    {
        _api = api;
    }

    // ── Commands ──────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusKind = "Running";
        StatusMessage = "Loading config from server...";

        try
        {
            var raw = await _api.GetCurrentRawAsync();
            var response = System.Text.Json.JsonSerializer
                .Deserialize<ConfigGetResponse>(raw, JsonStrict.Options);

            if (response?.Config is null)
                throw new Exception("Server returned an empty config.");

            _loaded = response.Config;
            PopulateFromDocument(_loaded);

            IsDirty = false;
            StatusKind = "Success";
            StatusMessage = $"Loaded: \"{_loaded.Name}\" (v{_loaded.SchemaVersion})";
        }
        catch (Exception ex)
        {
            StatusKind = "Error";
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
        
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusKind = "Running";
        StatusMessage = "Saving config...";

        try
        {
            var doc = BuildDocument();
            var http = new HttpClient { BaseAddress = new Uri(ServerUrl.TrimEnd('/') + "/") };

            // Wrap in ConfigPutRequest – server action expects this
            var request = new ConfigPutRequest(doc);

            // IMPORTANT: use default serializer options (PascalCase) to match ASP.NET default model binding
            var json = System.Text.Json.JsonSerializer.Serialize(request);

            System.Diagnostics.Debug.WriteLine($"[Save] URL: {http.BaseAddress}api/config/current");
            System.Diagnostics.Debug.WriteLine($"[Save] Payload length: {json.Length}");
            System.Diagnostics.Debug.WriteLine($"[Save] Payload preview: {json[..Math.Min(400, json.Length)]}");

            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var resp = await http.PutAsync("api/config/current", content);

            System.Diagnostics.Debug.WriteLine($"[Save] Status: {(int)resp.StatusCode} {resp.StatusCode}");
            var body = await resp.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[Save] Response body: {body}");

            resp.EnsureSuccessStatusCode();

            _loaded = doc;
            IsDirty = false;
            StatusKind = "Success";
            StatusMessage = $"Saved at {DateTimeOffset.UtcNow:HH:mm:ss} UTC";
        }
        catch (Exception ex)
        {
            StatusKind = "Error";
            StatusMessage = $"Save failed: {ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Save] Exception (outer): {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        ConnectionStatus = "Testing...";
        ConnectionStatusColor = "Gray";

        try
        {
            using var http = new HttpClient
            {
                BaseAddress = new Uri(ServerUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(5)
            };
            var resp = await http.GetAsync("api/config/current");
            if (resp.IsSuccessStatusCode)
            {
                ConnectionStatus = "✓ Connected";
                ConnectionStatusColor = "Green";
            }
            else
            {
                ConnectionStatus = $"✗ HTTP {(int)resp.StatusCode}";
                ConnectionStatusColor = "OrangeRed";
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"✗ {ex.Message}";
            ConnectionStatusColor = "Red";
        }
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        PopulateFromDocument(new StrategyConfigDocument());
        IsDirty = true;
        StatusKind = "Neutral";
        StatusMessage = "Defaults loaded — click Save to apply.";
    }

    // ── Helpers ───────────────────────────────────────────────

    private void PopulateFromDocument(StrategyConfigDocument doc)
    {
        var g = doc.Global;

        // Regime — Trend
        TrendR2Min = g.Regime.Trend.R2Min;
        TrendEmaSlopeAbsMin = g.Regime.Trend.EmaSlopeAbsMin;
        TrendWindowHours = g.Regime.Trend.WindowHours;

        // Regime — Sideways
        SidewaysVolNormMax = g.Regime.Sideways.VolNormMax;
        SidewaysR2Max = g.Regime.Sideways.R2Max;
        SidewaysBandContainPctMin = g.Regime.Sideways.BandContainPctMin;
        SidewaysWindowHours = g.Regime.Sideways.WindowHours;

        // Regime — Volatile
        VolatileVolNormMin = g.Regime.Volatile.VolNormMin;
        VolatileR2Max = g.Regime.Volatile.R2Max;

        // Scoring — Weights
        WeightRewardFee = g.Scoring.Reinvest.Weights.RewardFee;
        WeightRewardInRange = g.Scoring.Reinvest.Weights.RewardInRange;
        WeightRiskVol = g.Scoring.Reinvest.Weights.RiskVol;
        WeightRiskDrift = g.Scoring.Reinvest.Weights.RiskDrift;
        WeightRiskTrendPenalty = g.Scoring.Reinvest.Weights.RiskTrendPenalty;

        // Scoring — Ranges
        FeeAprMin = g.Scoring.Reinvest.FeeApr.Min;
        FeeAprMax = g.Scoring.Reinvest.FeeApr.Max;
        InRangePctMin = g.Scoring.Reinvest.InRangePct.Min;
        InRangePctMax = g.Scoring.Reinvest.InRangePct.Max;
        VolNormMin = g.Scoring.Reinvest.VolNorm.Min;
        VolNormMax = g.Scoring.Reinvest.VolNorm.Max;
        DriftTicksMinMultiplier = g.Scoring.Reinvest.DriftTicksPerHour.MinMultiplierOfTickSpacing;
        DriftTicksMaxMultiplier = g.Scoring.Reinvest.DriftTicksPerHour.MaxMultiplierOfTickSpacing;
        ScoreCentering = g.Scoring.Reinvest.ScoreCentering;

        // Scoring — Trend Penalty
        TrendPenaltySideways = g.Scoring.Reinvest.TrendPenalty.Sideways;
        TrendPenaltyTrending = g.Scoring.Reinvest.TrendPenalty.Trending;
        TrendPenaltyVolatile = g.Scoring.Reinvest.TrendPenalty.Volatile;

        // Decisions
        CompoundScoreMin = g.Decisions.CompoundScoreMin;
        ReallocateScoreMax = g.Decisions.ReallocateScoreMax;
        MinHoursBetweenCompounds = g.Decisions.MinHoursBetweenCompounds;
        StaleAfterSeconds = g.Decisions.StaleAfterSeconds;

        // Guardrails
        MaxSlippageBps = g.Guardrails.MaxSlippageBps;
        MaxNotionalUsdPerProposal = g.Guardrails.MaxNotionalUsdPerProposal;
        MaxGasFeeGwei = g.Guardrails.MaxGasFeeGwei;
        DeadlineSecondsAtExecution = g.Guardrails.DeadlineSecondsAtExecution;
        AllowApprovals = g.Guardrails.AllowApprovals;
        MaxApprovalUsd = g.Guardrails.MaxApprovalUsd;

        // Hedging
        HedgingEnabled = g.Hedging.Enabled;
        HedgingExposurePctTrigger = g.Hedging.ExposurePctTrigger;
        HedgeTrendDownR2Min = g.Hedging.TrendDown.R2Min;
        HedgeTrendDownEmaSlopeMax = g.Hedging.TrendDown.EmaSlopeMax;
        HedgeVolSpikeRatioMin = g.Hedging.VolSpike.Vol1hOverVol24hMin;
        HedgeSuggestTrendDownMin = g.Hedging.SuggestedHedgePct.TrendDown.Min;
        HedgeSuggestTrendDownMax = g.Hedging.SuggestedHedgePct.TrendDown.Max;
        HedgeSuggestVolSpikeMin = g.Hedging.SuggestedHedgePct.VolSpike.Min;
        HedgeSuggestVolSpikeMax = g.Hedging.SuggestedHedgePct.VolSpike.Max;
    }

    private StrategyConfigDocument BuildDocument()
    {
        var base_ = _loaded ?? new StrategyConfigDocument();

        return base_ with
        {
            UpdatedUtc = DateTimeOffset.UtcNow,
            Global = new GlobalStrategyConfig
            {
                Regime = new RegimeConfig
                {
                    Trend = new TrendRegimeConfig
                    {
                        R2Min = TrendR2Min,
                        EmaSlopeAbsMin = TrendEmaSlopeAbsMin,
                        WindowHours = TrendWindowHours
                    },
                    Sideways = new SidewaysRegimeConfig
                    {
                        VolNormMax = SidewaysVolNormMax,
                        R2Max = SidewaysR2Max,
                        BandContainPctMin = SidewaysBandContainPctMin,
                        WindowHours = SidewaysWindowHours
                    },
                    Volatile = new VolatileRegimeConfig
                    {
                        VolNormMin = VolatileVolNormMin,
                        R2Max = VolatileR2Max
                    },
                    PriorityOrder = base_.Global.Regime.PriorityOrder
                },
                Scoring = new ScoringConfig
                {
                    Reinvest = new ReinvestScoringConfig
                    {
                        FeeApr = new RangeDouble { Min = FeeAprMin, Max = FeeAprMax },
                        InRangePct = new RangeDouble { Min = InRangePctMin, Max = InRangePctMax },
                        VolNorm = new RangeDouble { Min = VolNormMin, Max = VolNormMax },
                        DriftTicksPerHour = new DriftTicksPerHourConfig
                        {
                            MinMultiplierOfTickSpacing = DriftTicksMinMultiplier,
                            MaxMultiplierOfTickSpacing = DriftTicksMaxMultiplier
                        },
                        Weights = new ReinvestWeights
                        {
                            RewardFee = WeightRewardFee,
                            RewardInRange = WeightRewardInRange,
                            RiskVol = WeightRiskVol,
                            RiskDrift = WeightRiskDrift,
                            RiskTrendPenalty = WeightRiskTrendPenalty
                        },
                        TrendPenalty = new TrendPenaltyConfig
                        {
                            Sideways = TrendPenaltySideways,
                            Trending = TrendPenaltyTrending,
                            Volatile = TrendPenaltyVolatile
                        },
                        ScoreCentering = ScoreCentering
                    }
                },
                Decisions = new DecisionConfig
                {
                    CompoundScoreMin = CompoundScoreMin,
                    ReallocateScoreMax = ReallocateScoreMax,
                    MinHoursBetweenCompounds = MinHoursBetweenCompounds,
                    StaleAfterSeconds = StaleAfterSeconds
                },
                Guardrails = new GuardrailsConfig
                {
                    MaxSlippageBps = MaxSlippageBps,
                    MaxNotionalUsdPerProposal = MaxNotionalUsdPerProposal,
                    MaxGasFeeGwei = MaxGasFeeGwei,
                    DeadlineSecondsAtExecution = DeadlineSecondsAtExecution,
                    AllowApprovals = AllowApprovals,
                    MaxApprovalUsd = MaxApprovalUsd
                },
                Hedging = new HedgingConfig
                {
                    Enabled = HedgingEnabled,
                    ExposurePctTrigger = HedgingExposurePctTrigger,
                    TrendDown = new TrendDownHedgeConfig
                    {
                        R2Min = HedgeTrendDownR2Min,
                        EmaSlopeMax = HedgeTrendDownEmaSlopeMax
                    },
                    VolSpike = new VolSpikeHedgeConfig
                    {
                        Vol1hOverVol24hMin = HedgeVolSpikeRatioMin
                    },
                    SuggestedHedgePct = new SuggestedHedgePctConfig
                    {
                        TrendDown = new PctRange
                        {
                            Min = HedgeSuggestTrendDownMin,
                            Max = HedgeSuggestTrendDownMax
                        },
                        VolSpike = new PctRange
                        {
                            Min = HedgeSuggestVolSpikeMin,
                            Max = HedgeSuggestVolSpikeMax
                        }
                    }
                }
            },
            Overrides = base_.Overrides
        };
    }

    // Mark dirty whenever any property changes
    partial void OnTrendR2MinChanged(double value) => IsDirty = true;
    partial void OnTrendEmaSlopeAbsMinChanged(double value) => IsDirty = true;
    partial void OnTrendWindowHoursChanged(int value) => IsDirty = true;
    partial void OnSidewaysVolNormMaxChanged(double value) => IsDirty = true;
    partial void OnSidewaysR2MaxChanged(double value) => IsDirty = true;
    partial void OnSidewaysBandContainPctMinChanged(double value) => IsDirty = true;
    partial void OnSidewaysWindowHoursChanged(int value) => IsDirty = true;
    partial void OnVolatileVolNormMinChanged(double value) => IsDirty = true;
    partial void OnVolatileR2MaxChanged(double value) => IsDirty = true;
    partial void OnWeightRewardFeeChanged(double value) => IsDirty = true;
    partial void OnWeightRewardInRangeChanged(double value) => IsDirty = true;
    partial void OnWeightRiskVolChanged(double value) => IsDirty = true;
    partial void OnWeightRiskDriftChanged(double value) => IsDirty = true;
    partial void OnWeightRiskTrendPenaltyChanged(double value) => IsDirty = true;
    partial void OnFeeAprMinChanged(double value) => IsDirty = true;
    partial void OnFeeAprMaxChanged(double value) => IsDirty = true;
    partial void OnInRangePctMinChanged(double value) => IsDirty = true;
    partial void OnInRangePctMaxChanged(double value) => IsDirty = true;
    partial void OnVolNormMinChanged(double value) => IsDirty = true;
    partial void OnVolNormMaxChanged(double value) => IsDirty = true;
    partial void OnDriftTicksMinMultiplierChanged(int value) => IsDirty = true;
    partial void OnDriftTicksMaxMultiplierChanged(int value) => IsDirty = true;
    partial void OnScoreCenteringChanged(int value) => IsDirty = true;
    partial void OnTrendPenaltySidewaysChanged(int value) => IsDirty = true;
    partial void OnTrendPenaltyTrendingChanged(int value) => IsDirty = true;
    partial void OnTrendPenaltyVolatileChanged(int value) => IsDirty = true;
    partial void OnCompoundScoreMinChanged(int value) => IsDirty = true;
    partial void OnReallocateScoreMaxChanged(int value) => IsDirty = true;
    partial void OnMinHoursBetweenCompoundsChanged(int value) => IsDirty = true;
    partial void OnStaleAfterSecondsChanged(int value) => IsDirty = true;
    partial void OnMaxSlippageBpsChanged(int value) => IsDirty = true;
    partial void OnMaxNotionalUsdPerProposalChanged(decimal value) => IsDirty = true;
    partial void OnMaxGasFeeGweiChanged(int value) => IsDirty = true;
    partial void OnDeadlineSecondsAtExecutionChanged(int value) => IsDirty = true;
    partial void OnAllowApprovalsChanged(bool value) => IsDirty = true;
    partial void OnMaxApprovalUsdChanged(decimal value) => IsDirty = true;
    partial void OnHedgingEnabledChanged(bool value) => IsDirty = true;
    partial void OnHedgingExposurePctTriggerChanged(double value) => IsDirty = true;
    partial void OnHedgeTrendDownR2MinChanged(double value) => IsDirty = true;
    partial void OnHedgeTrendDownEmaSlopeMaxChanged(double value) => IsDirty = true;
    partial void OnHedgeVolSpikeRatioMinChanged(double value) => IsDirty = true;
    partial void OnHedgeSuggestTrendDownMinChanged(double value) => IsDirty = true;
    partial void OnHedgeSuggestTrendDownMaxChanged(double value) => IsDirty = true;
    partial void OnHedgeSuggestVolSpikeMinChanged(double value) => IsDirty = true;
    partial void OnHedgeSuggestVolSpikeMaxChanged(double value) => IsDirty = true;
}