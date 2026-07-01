using System.Windows;
using KeyboardManager.Converters;
using KeyboardManager.Models;
using KeyboardManager.Services;
using KeyboardManager.Services.Configuration;
using KeyboardManager.Services.Elevation;
using KeyboardManager.ViewModels;

namespace KeyboardManager;

/// <summary>
/// Interaction logic for MainWindow.xaml. The window holds UI state (selection,
/// busy flag), asks the user for confirmation before destructive operations, and
/// renders <see cref="OperationResult"/>s. The operation flows themselves live in
/// <see cref="LayoutOperations"/> — see ADR-0002.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly LayoutOperations _operations;
    private readonly LayoutRemovalService _removal;
    private readonly KeyboardManagerConfig _config;

    public MainWindow()
    {
        var registry = new WindowsKeyboardLayoutRegistry();
        var resolver = new LayoutResolver(registry);
        var applier = new SessionLayoutApplier();
        _removal = new LayoutRemovalService(registry, new ElevatedOperationRunner());
        _operations = new LayoutOperations(
            new BackupService(),
            _removal,
            new LayoutResetService(registry, applier),
            applier);
        _config = KeyboardManagerConfig.Load();
        _vm = new MainViewModel(resolver);

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

        // Declared layouts are removable via Settings — warn before reaching for the registry.
        if (entry.Status == LayoutStatus.Declared)
        {
            var msg = $"'{entry.DisplayName}' is a Declared layout — Windows Settings can remove it.\n" +
                      "Remove anyway via the registry?";
            if (MessageBox.Show(this, msg, "Remove declared layout",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;
        }

        // Show the concrete targets and confirm.
        var plan = _removal.PlanRemoval(entry);
        if (!ConfirmRemoval(entry, plan))
            return;

        RunOperation($"Remove '{entry.DisplayName}'", () => _operations.Remove(entry));
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

        RunOperation("Reset", () => _operations.Reset(_config));
    }

    private void OnBackupNow(object sender, RoutedEventArgs e)
        => RunOperation("Backup", _operations.Backup);

    // ───────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Toggle busy, run an operation, render its result. This is the single
    /// place that touches <see cref="MainViewModel.IsBusy"/> and the single place
    /// that turns an <see cref="OperationResult"/> into a dialog + status text.
    /// </summary>
    private void RunOperation(string label, Func<OperationResult> operation)
    {
        _vm.IsBusy = true;
        OperationResult result;
        try
        {
            result = operation();
        }
        catch (Exception ex)
        {
            _vm.IsBusy = false;
            MessageBox.Show(this, $"{label} threw an exception:\n\n{ex.Message}",
                label, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            _vm.IsBusy = false;
        }

        RenderResult(label, result);
        Refresh();
    }

    /// <summary>
    /// Turn an <see cref="OperationResult"/> into a MessageBox + status-bar text.
    /// The one and only place that maps result data to user-facing output.
    /// </summary>
    private void RenderResult(string label, OperationResult r)
    {
        var lines = new List<string> { r.Summary };

        if (r.BackupPath is not null)
            lines.Add($"Backup: {r.BackupPath}");

        if (r.NeedsSignOut)
            lines.Add("\nSign out and back in for the change to take full effect.");

        foreach (var note in r.Notes)
            lines.Add($"\n{note}");

        if (r.Errors.Count > 0)
            lines.Add("\nErrors:\n" + string.Join("\n", r.Errors));

        var icon = r.Success ? MessageBoxImage.Information : MessageBoxImage.Warning;
        MessageBox.Show(this, string.Join("\n", lines), label, MessageBoxButton.OK, icon);

        SetStatus(r.Summary);
    }

    private bool ConfirmRemoval(ResolvedLayout entry, RemovalPlan plan)
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

    private void UpdateDefaultSetLabel()
    {
        var target = LayoutResetService.DescribeTarget(_config);
        var tag = _config.SourcePath is null ? "built-in" : "config";
        DefaultSetText.Text = $"Reset target ({tag}): {target}";
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
