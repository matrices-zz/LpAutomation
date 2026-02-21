using System;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using LpAutomation.Desktop.Services;

namespace LpAutomation.Desktop.ViewModels;

/// <summary>
/// UI row model for RecommendationsPageView.
/// Kept intentionally simple: the page VM populates most fields.
/// </summary>
public sealed class PoolRecommendationRowViewModel
{
    // ===== Layout =====
    public bool IsCompact { get; set; }
    public Thickness RowPadding => IsCompact ? new Thickness(10, 8, 10, 8) : new Thickness(12, 10, 12, 10);

    // ===== Pool identity =====
    public int ChainId { get; set; }
    public string Token0 { get; set; } = "";
    public string Token1 { get; set; } = "";
    public int FeeTier { get; set; }
    public string Dex { get; set; } = "";
    public string ProtocolVersion { get; set; } = "";
    public string PoolAddress { get; set; } = "";
    public string PoolAddressShort { get; set; } = "";

    public string PoolName { get; set; } = "";
    public string PoolMeta { get; set; } = "";

    // ===== Regime / action =====
    public string RegimeLabel { get; set; } = "";
    public Brush RegimeBrush { get; set; } = Brushes.Gray;

    public string ActionLabel { get; set; } = "";
    public Brush ActionBrush { get; set; } = Brushes.Gray;

    // ===== Sentiment =====
    public string SentimentLabel { get; set; } = "";
    public string SentimentDetail { get; set; } = "";

    // ===== Scores =====
    public int Confidence { get; set; }
    public string ConfidenceLabel { get; set; } = "";

    public int ReinvestScore { get; set; }
    public string ReinvestScoreLabel { get; set; } = "";
    public string ReinvestRationale { get; set; } = "";

    public int ReallocateScore { get; set; }
    public string ReallocateScoreLabel { get; set; } = "";

    // ===== Heat + momentum =====
    public int Heat { get; set; }
    public string HeatText { get; set; } = "";
    public string HeatBreakdownDisplay { get; set; } = "";
    public string HeatTooltip { get; set; } = "";

    public string HeatArrowGlyph { get; set; } = "↕";
    public string HeatDeltaDisplay { get; set; } = "";

    public string MomentumArrowGlyph { get; set; } = "↕";
    public string MomentumDeltaDisplay { get; set; } = "";
    public string MomentumToolTip { get; set; } = "";

    // ===== Time =====
    public DateTimeOffset CreatedUtc { get; set; }
    public string UpdatedAgo { get; set; } = "";
    public string SnapshotTimeDisplay { get; set; } = "";

    public static PoolRecommendationRowViewModel FromDto(RecommendationDto dto)
    {
        var vm = new PoolRecommendationRowViewModel
        {
            ChainId = dto.ChainId,
            Token0 = dto.Token0 ?? "",
            Token1 = dto.Token1 ?? "",
            FeeTier = dto.FeeTier,
            Dex = dto.Dex ?? "",
            ProtocolVersion = dto.ProtocolVersion ?? "",
            PoolAddress = dto.PoolAddress ?? "",
            PoolName = $"{dto.Token0}/{dto.Token1} ({dto.FeeTier})",
            CreatedUtc = dto.CreatedUtc
        };

        vm.PoolAddressShort = ShortAddress(vm.PoolAddress);
        vm.PoolMeta = BuildMeta(vm);

        // Fill baseline heat/momentum from DetailsJson. Deltas/arrows are managed by the page VM.
        if (!string.IsNullOrWhiteSpace(dto.DetailsJson))
            TryPopulateHeatAndMomentum(vm, dto.DetailsJson!);

        vm.SnapshotTimeDisplay = vm.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        vm.UpdatedAgo = ToAgo(vm.CreatedUtc, DateTimeOffset.UtcNow);

        vm.ConfidenceLabel = $"{vm.Confidence}/100";
        vm.ReinvestScoreLabel = $"{vm.ReinvestScore}/100";
        vm.ReallocateScoreLabel = $"{vm.ReallocateScore}/100";

        return vm;
    }

    private static string BuildMeta(PoolRecommendationRowViewModel vm)
    {
        var dex = string.IsNullOrWhiteSpace(vm.Dex) ? "" : vm.Dex.Trim();
        var pv = string.IsNullOrWhiteSpace(vm.ProtocolVersion) ? "" : vm.ProtocolVersion.Trim();
        var proto = string.IsNullOrWhiteSpace(pv) ? dex : $"{dex} {pv}";
        proto = string.IsNullOrWhiteSpace(proto) ? "" : proto + " • ";
        return $"{proto}Chain {vm.ChainId}";
    }

    private static string ShortAddress(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return "";
        var s = addr.Trim();
        if (s.Length <= 12) return s;
        return s.Substring(0, 6) + "…" + s.Substring(s.Length - 4);
    }

    private static void TryPopulateHeatAndMomentum(PoolRecommendationRowViewModel vm, string detailsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(detailsJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("heat", out var heatEl))
            {
                if (heatEl.TryGetProperty("blended", out var blendedEl) && blendedEl.TryGetInt32(out var blended))
                {
                    vm.Heat = blended;
                    vm.HeatText = $"Heat {blended}/100";
                }

                int? t = TryGetInt(heatEl, "tactical");
                int? s = TryGetInt(heatEl, "structural");
                int? m = TryGetInt(heatEl, "macro");
                int? sm = TryGetInt(heatEl, "superMacro");

                vm.HeatBreakdownDisplay = $"T{t?.ToString() ?? "-"} S{s?.ToString() ?? "-"} M{m?.ToString() ?? "-"} SM{sm?.ToString() ?? "-"}";
                vm.HeatTooltip =
                    $"Heat {vm.Heat}/100 (Blended)\n" +
                    $"Tactical:   {t?.ToString() ?? "-"}\n" +
                    $"Structural: {s?.ToString() ?? "-"}\n" +
                    $"Macro:      {m?.ToString() ?? "-"}\n" +
                    $"SuperMacro: {sm?.ToString() ?? "-"}";
            }

            if (root.TryGetProperty("ret", out var retEl))
            {
                // We want momentum as 15m vs 1h (institutional-style "short vs baseline")
                // Server gives log-returns; convert to % for UI friendliness.
                double? m15Log = TryGetDouble(retEl, "m15");
                double? h1Log = TryGetDouble(retEl, "h1");

                double? m15Pct = m15Log is null ? null : (Math.Exp(m15Log.Value) - 1.0) * 100.0;
                double? h1Pct = h1Log is null ? null : (Math.Exp(h1Log.Value) - 1.0) * 100.0;

                // Delta = 15m drift minus 1h drift. Positive = accelerating up vs baseline.
                double? deltaPct = (m15Pct is not null && h1Pct is not null) ? (m15Pct.Value - h1Pct.Value) : null;

                // Arrow uses delta when possible; otherwise fall back to 15m direction.
                var arrowSignal = deltaPct ?? m15Pct;

                if (arrowSignal is not null)
                {
                    // eps is now in % units (since arrowSignal is %)
                    vm.MomentumArrowGlyph = ArrowFromDelta(arrowSignal.Value, eps: 0.20);
                    vm.MomentumDeltaDisplay = deltaPct is null ? "" : FormatSigned(deltaPct, decimals: 1, suffix: "%");

                    vm.MomentumToolTip =
                        "Price momentum (15m vs 1h)\n" +
                        $"15m: {FormatSigned(m15Pct, decimals: 2, suffix: "%")}\n" +
                        $"1h:  {FormatSigned(h1Pct, decimals: 2, suffix: "%")}\n" +
                        $"Δ:   {FormatSigned(deltaPct, decimals: 2, suffix: "%")}";
                }
                else
                {
                    vm.MomentumArrowGlyph = "↕";
                    vm.MomentumDeltaDisplay = "";
                    vm.MomentumToolTip = "Price momentum (15m vs 1h)\ninsufficient data";
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private static int? TryGetInt(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
            return v;
        return null;
    }

    private static double? TryGetDouble(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var v))
            return v;
        return null;
    }

    private static string ArrowFromDelta(double delta, double eps = 0.0025)
    {
        if (delta > eps) return "▲";
        if (delta < -eps) return "▼";
        return "↕";
    }

    private static string FormatSigned(double? v, int decimals = 3, string suffix = "")
    {
        if (v is null) return "-";

        // Build a format like "0.00" based on decimals requested
        var fmt = "0" + (decimals > 0 ? "." + new string('0', decimals) : "");

        var s = v.Value >= 0
            ? "+" + v.Value.ToString(fmt, CultureInfo.InvariantCulture)
            : v.Value.ToString(fmt, CultureInfo.InvariantCulture);

        return s + suffix;
    }

    private static string ToAgo(DateTimeOffset utc, DateTimeOffset nowUtc)
    {
        var span = nowUtc - utc;
        if (span.TotalSeconds < 60) return $"{(int)Math.Floor(span.TotalSeconds)}s ago";
        if (span.TotalMinutes < 60) return $"{(int)Math.Floor(span.TotalMinutes)}m ago";
        if (span.TotalHours < 24) return $"{(int)Math.Floor(span.TotalHours)}h ago";
        return $"{(int)Math.Floor(span.TotalDays)}d ago";
    }
}