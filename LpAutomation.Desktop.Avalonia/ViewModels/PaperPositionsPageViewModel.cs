using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LpAutomation.Contracts.PaperPositions;
using LpAutomation.Desktop.Avalonia.Services;

namespace LpAutomation.Desktop.Avalonia.ViewModels;

public partial class PaperPositionsPageViewModel : ObservableObject
{
    private readonly PaperPositionsApiClient _api;

    public PaperPositionsPageViewModel(PaperPositionsApiClient api)
    {
        _api = api;
        Rows = new ObservableCollection<PaperPositionDto>();

        Status = "Ready";
        OwnerTagFilter = "eddie-dev";

        OwnerTag = "eddie-dev";
        ChainIdText = "1";
        Dex = "UniswapV3";
        PoolAddress = "0x88e6A0c2dDD26FEEb64F039a2c41296FcB3f5640";
        Token0Symbol = "USDC";
        Token1Symbol = "WETH";
        FeeTierText = "500";
        LiquidityNotionalUsdText = "2500";
        EntryPriceText = "3500";
        TickLowerText = "190000";
        TickUpperText = "210000";
        Enabled = true;
        Notes = "paper test position";
    }

    public ObservableCollection<PaperPositionDto> Rows { get; }

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _ownerTagFilter = "";

    [ObservableProperty] private string _ownerTag = "";
    [ObservableProperty] private string _chainIdText = "1";
    [ObservableProperty] private string _dex = "UniswapV3";
    [ObservableProperty] private string _poolAddress = "";
    [ObservableProperty] private string _token0Symbol = "";
    [ObservableProperty] private string _token1Symbol = "";
    [ObservableProperty] private string _feeTierText = "500";
    [ObservableProperty] private string _liquidityNotionalUsdText = "2500";
    [ObservableProperty] private string _entryPriceText = "3500";
    [ObservableProperty] private string _tickLowerText = "190000";
    [ObservableProperty] private string _tickUpperText = "210000";
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _notes = "";

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Status = "Loading paper positions...";
            var rows = await _api.ListAsync(OwnerTagFilter, 200, ct);

            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);

            Status = $"Loaded {Rows.Count} paper position(s).";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateAsync(CancellationToken ct)
    {
        try
        {
            if (!TryParseInputs(out var request, out var error))
            {
                Status = error;
                return;
            }

            Status = "Creating paper position...";
            _ = await _api.CreateAsync(request!, ct);
            await RefreshAsync(ct);
            Status = "Paper position created.";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteByIdAsync(Guid id, CancellationToken ct)
    {
        try
        {
            Status = $"Deleting {id}...";
            var deleted = await _api.DeleteAsync(id, ct);
            await RefreshAsync(ct);
            Status = deleted ? "Paper position deleted." : "Paper position not found.";
        }
        catch (Exception ex)
        {
            Status = $"ERROR: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private bool TryParseInputs(out UpsertPaperPositionRequest? req, out string error)
    {
        req = null;

        if (!int.TryParse(ChainIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chainId))
        {
            error = "ChainId must be an integer.";
            return false;
        }

        if (!int.TryParse(FeeTierText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var feeTier))
        {
            error = "FeeTier must be an integer.";
            return false;
        }

        if (!decimal.TryParse(LiquidityNotionalUsdText, NumberStyles.Number, CultureInfo.InvariantCulture, out var liquidity))
        {
            error = "LiquidityNotionalUsd must be a decimal number.";
            return false;
        }

        if (!decimal.TryParse(EntryPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var entryPrice))
        {
            error = "EntryPrice must be a decimal number.";
            return false;
        }

        if (!int.TryParse(TickLowerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tickLower))
        {
            error = "TickLower must be an integer.";
            return false;
        }

        if (!int.TryParse(TickUpperText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tickUpper))
        {
            error = "TickUpper must be an integer.";
            return false;
        }

        req = new UpsertPaperPositionRequest(
            OwnerTag: OwnerTag.Trim(),
            ChainId: chainId,
            Dex: Dex.Trim(),
            PoolAddress: PoolAddress.Trim(),
            Token0Symbol: Token0Symbol.Trim(),
            Token1Symbol: Token1Symbol.Trim(),
            FeeTier: feeTier,
            LiquidityNotionalUsd: liquidity,
            EntryPrice: entryPrice,
            TickLower: tickLower,
            TickUpper: tickUpper,
            Enabled: Enabled,
            Notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());

        error = string.Empty;
        return true;
    }
}
