using System.Windows;
using KeyboardManager.Converters;
using KeyboardManager.Models;
using KeyboardManager.Services;
using KeyboardManager.Services.Configuration;
using KeyboardManager.Services.Elevation;
using KeyboardManager.ViewModels;

namespace KeyboardManager;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the <see cref="MainViewModel"/> to
/// the controls and routes button clicks through the registry services.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly BackupService _backup;
    private readonly LayoutRemovalService _removal;
    private readonly SessionLayoutApplier _applier;
    private readonly LayoutInspector _inspector;
    private readonly LayoutResetService _reset;
    private readonly KeyboardManagerConfig _config;

    public MainWindow()
    {
        var registry = new WindowsKeyboardLayoutRegistry();
        _inspector = new LayoutInspector(registry);
        _backup = new BackupService();
        _applier = new SessionLayoutApplier();
        _removal = new LayoutRemovalService(registry, new ElevatedOperationRunner());
        _config = KeyboardManagerConfig.Load();
        _reset = new LayoutResetService(registry, _applier);
        _vm = new MainViewModel(_inspector);

        InitializeComponent();
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            UpdateDefaultSetLabel();
            Refresh();
        };
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        var entry = _vm.SelectedEntry;
        if (entry is null)
        {
            SetStatus("Select a layout to remove first.");
            return;
        }

        if (entry.Status == LayoutStatus.Declared)
        {
            var msg = $"'{entry.DisplayName}' is a Declared layout — Windows Settings can remove it.\n" +
                      "Remove anyway via the registry?";
            if (MessageBox.Show(this, msg, "Remove declared layout",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
        }

        var plan = _removal.PlanRemoval(entry);
        if (!ConfirmRemoval(entry, plan))
            return;

        // Layer 1: backup before touching anything.
        _vm.IsBusy = true;
        BackupResult? backup = null;
        try
        {
            backup = _backup.BackupAll($"remove-{entry.LayoutId}");
        }
        catch (Exception ex)
        {
            _vm.IsBusy = false;
            MessageBox.Show(this,
                $"Backup failed — aborting. Nothing was changed.\n\n{ex.Message}",
                "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Layer 2: execute the deletion.
        RemovalResult result;
        try
        {
            result = _removal.Execute(plan);
        }
        catch (Exception ex)
        {
            _vm.IsBusy = false;
            MessageBox.Show(this,
                $"Removal threw an exception. The backup is at:\n{backup.Path}\n\n{ex.Message}",
                "Removal failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Best-effort apply.
        _applier.TryUnload(entry.LayoutId);

        _vm.IsBusy = false;
        Refresh();

        // Layer 3: honest post-op guidance.
        var signOutNote = plan.NeedsElevation
            ? "\n\nSign out and back in for .DEFAULT-sourced ghosts to fully clear."
            : string.Empty;

        var backupNote = backup.SkippedKeys.Count > 0
            ? $"\n\nNote: {backup.SkippedKeys.Count} key(s) were skipped during backup (likely .DEFAULT without elevation)."
            : string.Empty;

        var errorsNote = result.Errors.Count > 0
            ? "\n\nErrors:\n" + string.Join("\n", result.Errors)
            : string.Empty;

        MessageBox.Show(this,
            $"Removed {result.Applied} of {result.Total} value(s).\n" +
            $"Backup: {backup.Path}{signOutNote}{backupNote}{errorsNote}",
            "Removal complete", MessageBoxButton.OK, MessageBoxImage.Information);

        SetStatus($"Removed {result.Applied}/{result.Total} for '{entry.DisplayName}'. Backup: {backup.Path}");
    }

    private bool ConfirmRemoval(LayoutEntry entry, RemovalPlan plan)
    {
        var lines = new List<string> { $"Delete '{entry.DisplayName}' from:" };
        foreach (var t in plan.LocalDeletes.Concat(plan.ElevatedDeletes))
            lines.Add($"  • {SourcesToTextConverter.SourceLabel(t.Kind, t.ValueName)}");

        if (plan.NeedsElevation)
            lines.Add("\nThis will trigger a UAC prompt (touches .DEFAULT).");

        lines.Add("\nA .reg backup is taken first.");

        return MessageBox.Show(this,
            string.Join("\n", lines),
            "Confirm removal",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning) == MessageBoxResult.OK;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        var target = LayoutResetService.DescribeTarget(_config);
        var source = _config.SourcePath is null
            ? "built-in default (no config file found)"
            : $"config: {_config.SourcePath}";

        var msg = $"Reset HKCU to the default set?\n\n" +
                  $"Target: {target}\n" +
                  $"Source: {source}\n\n" +
                  $"This clears HKCU\\Keyboard Layout\\Preload and Substitutes, then writes the defaults above.\n" +
                  $".DEFAULT is NOT touched.\n\n" +
                  $"A .reg backup is taken first.";
        if (MessageBox.Show(this, msg, "Confirm reset",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
            return;

        _vm.IsBusy = true;
        BackupResult? backup = null;
        try
        {
            backup = _backup.BackupAll("reset");
        }
        catch (Exception ex)
        {
            _vm.IsBusy = false;
            MessageBox.Show(this,
                $"Backup failed — aborting. Nothing was changed.\n\n{ex.Message}",
                "Backup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            _reset.Reset(_config);
        }
        catch (Exception ex)
        {
            _vm.IsBusy = false;
            MessageBox.Show(this,
                $"Reset threw an exception. Backup: {backup.Path}\n\n{ex.Message}",
                "Reset failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _applier.BroadcastSettingsChange();
        _vm.IsBusy = false;
        Refresh();

        MessageBox.Show(this,
            $"HKCU reset to: {target}\n\n" +
            $"Backup: {backup.Path}\n\n" +
            $"Sign out and back in for the change to take full effect.",
            "Reset complete", MessageBoxButton.OK, MessageBoxImage.Information);

        SetStatus($"Reset to default. Backup: {backup.Path}");
    }

    private void UpdateDefaultSetLabel()
    {
        var target = LayoutResetService.DescribeTarget(_config);
        var tag = _config.SourcePath is null ? "built-in" : "config";
        DefaultSetText.Text = $"Reset target ({tag}): {target}";
    }

    private void OnBackupNow(object sender, RoutedEventArgs e)
    {
        // Filled by issue 08.
        SetStatus("Backup is not implemented yet (issue 08).");
    }

    private void Refresh()
    {
        _vm.IsBusy = true;
        try
        {
            _vm.Refresh();
            SetStatus($"Loaded {_vm.Layouts.Count} layout(s).");
        }
        catch (Exception ex)
        {
            SetStatus($"Refresh failed: {ex.Message}");
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
