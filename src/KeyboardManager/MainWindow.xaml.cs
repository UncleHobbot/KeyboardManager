using System.Windows;
using KeyboardManager.Converters;
using KeyboardManager.Models;
using KeyboardManager.Services;
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

    public MainWindow()
    {
        var registry = new WindowsKeyboardLayoutRegistry();
        _inspector = new LayoutInspector(registry);
        _backup = new BackupService();
        _removal = new LayoutRemovalService(registry, new ElevatedOperationRunner());
        _applier = new SessionLayoutApplier();
        _vm = new MainViewModel(_inspector);

        InitializeComponent();
        DataContext = _vm;

        Loaded += (_, _) => Refresh();
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
        // Filled by issue 06.
        SetStatus("Reset is not implemented yet (issue 06).");
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
