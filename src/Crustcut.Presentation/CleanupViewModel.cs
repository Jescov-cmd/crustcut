using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Bloatware;

namespace Crustcut.Presentation;

public sealed class BloatwareItemRowVm : ObservableObject
{
    public BloatwareItem Item { get; }

    public string DisplayName => Item.Entry.DisplayName;
    public string AppxName => Item.Entry.AppxName;
    public string Category => Item.Entry.Category;
    public string TierLabel => Item.Entry.Tier.ToString();
    public bool CanUninstall => Item.Entry.Tier != SafetyTier.Blocked;
    public bool IsRisky => Item.Entry.Tier == SafetyTier.Risky;
    public string? RiskNote => Item.Entry.RiskNote;

    private string _statusText = "Installed";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    public BloatwareItemRowVm(BloatwareItem item) => Item = item;
}

public sealed class DesktopBloatRowVm
{
    public DesktopBloatHit Hit { get; }
    public DesktopBloatRowVm(DesktopBloatHit hit) => Hit = hit;
    public string DisplayName => Hit.Entry.DisplayName;
    public string ProgramName => Hit.Program.DisplayName;
    public string Publisher => Hit.Program.Publisher ?? "";
    public string TierLabel => Hit.Entry.Tier.ToString();
    public string? RiskNote => Hit.Entry.RiskNote;
}

/// <summary>
/// Cleanup page (was "Bloatware"). Nothing is ever removed without an explicit per-item
/// confirmation — see <see cref="UninstallAsync"/>.
/// </summary>
public partial class CleanupViewModel : ObservableObject
{
    private readonly BloatwareDetector _detector;
    private readonly IInstalledProgramsClient _programs;
    private readonly IReadOnlyList<DesktopBloatwareCatalogEntry> _desktopCatalog;
    private readonly BloatwareUninstallService _uninstall;
    private readonly BloatwareDisableService _disable;
    private readonly IDialogService _dialogs;

    public ObservableCollection<BloatwareItemRowVm> Items { get; } = new();
    public ObservableCollection<DesktopBloatRowVm> DesktopItems { get; } = new();

    [ObservableProperty] private string _status = "Scan to find apps you probably don't need.";
    [ObservableProperty] private int _detectedCount;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasDesktopItems;

    public CleanupViewModel(
        BloatwareDetector detector,
        IInstalledProgramsClient programs,
        IReadOnlyList<DesktopBloatwareCatalogEntry> desktopCatalog,
        BloatwareUninstallService uninstall,
        BloatwareDisableService disable,
        IDialogService dialogs)
    {
        _detector = detector;
        _programs = programs;
        _desktopCatalog = desktopCatalog;
        _uninstall = uninstall;
        _disable = disable;
        _dialogs = dialogs;
    }

    public async Task RefreshAsync()
    {
        IsScanning = true;
        Status = "Scanning installed packages…";
        try
        {
            var items = await _detector.DetectAsync();
            Items.Clear();
            foreach (var i in items) Items.Add(new BloatwareItemRowVm(i));

            // Desktop (Win32) programs — detection only; the action opens the program's own
            // uninstaller. The registry scan runs off the UI thread; it touches many keys.
            var desktopHits = await Task.Run(() =>
                DesktopBloatwareDetector.Match(_programs.ListInstalled(), _desktopCatalog));
            DesktopItems.Clear();
            foreach (var hit in desktopHits) DesktopItems.Add(new DesktopBloatRowVm(hit));
            HasDesktopItems = DesktopItems.Count > 0;

            DetectedCount = items.Count + desktopHits.Count;
            Status = DetectedCount == 0
                ? "Nothing unnecessary found."
                : $"{items.Count} Store app(s) + {desktopHits.Count} desktop program(s) found.";
        }
        catch (Exception ex)
        {
            Status = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Uninstalls one app, but only after the user explicitly confirms. Never batch, never
    /// implicit — removal is irreversible from the app's point of view.
    /// </summary>
    public async Task UninstallAsync(BloatwareItemRowVm row)
    {
        if (!row.CanUninstall)
        {
            await _dialogs.ShowAsync(row.DisplayName,
                "Windows blocks removing this one — it's required by the system.", DialogKind.Warning);
            return;
        }

        var warning = row.RiskNote is null ? "" : $"\n\n{row.RiskNote}";
        var confirmed = await _dialogs.ConfirmAsync(
            $"Remove {row.DisplayName}?",
            $"This uninstalls {row.DisplayName} for the current user. You can reinstall it " +
            $"from the Microsoft Store later.{warning}",
            "Remove");

        if (!confirmed) return;

        row.IsBusy = true;
        try
        {
            await _uninstall.UninstallAsync(row.Item);
            Items.Remove(row);
            DetectedCount = Items.Count + DesktopItems.Count;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(row.DisplayName, $"Uninstall failed: {ex.Message}", DialogKind.Error);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    /// <summary>Disables an app instead of removing it — reversible, so no confirmation gate.</summary>
    public async Task DisableAsync(BloatwareItemRowVm row)
    {
        row.IsBusy = true;
        try
        {
            await _disable.DisableAsync(row.Item);
            Items.Remove(row);
            DetectedCount = Items.Count + DesktopItems.Count;
        }
        catch (Exception ex)
        {
            await _dialogs.ShowAsync(row.DisplayName, $"Disable failed: {ex.Message}", DialogKind.Error);
        }
        finally
        {
            row.IsBusy = false;
        }
    }
}
