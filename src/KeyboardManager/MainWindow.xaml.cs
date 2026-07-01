using System.Windows;
using KeyboardManager.Services;
using KeyboardManager.ViewModels;

namespace KeyboardManager;

/// <summary>
/// Interaction logic for MainWindow.xaml. Wires the <see cref="MainViewModel"/> to
/// the controls and routes button clicks. Destructive operations (Remove, Reset,
/// Backup now) are filled in by later issues; Refresh works now.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        var registry = new WindowsKeyboardLayoutRegistry();
        var inspector = new LayoutInspector(registry);
        _vm = new MainViewModel(inspector);

        InitializeComponent();
        DataContext = _vm;

        Loaded += (_, _) => Refresh();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        // Filled by issue 05.
        SetStatus("Remove is not implemented yet (issue 05).");
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
