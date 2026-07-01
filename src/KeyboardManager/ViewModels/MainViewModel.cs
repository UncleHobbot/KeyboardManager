using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyboardManager.Models;
using KeyboardManager.Services;

namespace KeyboardManager.ViewModels;

/// <summary>
/// View model for <see cref="MainWindow"/>. Holds the layout list and the
/// command-ish methods the window binds to. Selection is tracked here so that
/// Remove/Reset can consult it.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LayoutInspector _inspector;
    private LayoutEntry? _selectedEntry;
    private bool _isBusy;

    public MainViewModel(LayoutInspector inspector)
    {
        _inspector = inspector;
    }

    /// <summary>
    /// The flat, ghost-first list of resolved layouts bound to the DataGrid.
    /// </summary>
    public ObservableCollection<LayoutEntry> Layouts { get; } = new();

    public LayoutEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_selectedEntry != value)
            {
                _selectedEntry = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRemove));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Remove is enabled only when a row is selected and we're not mid-operation.
    /// </summary>
    public bool CanRemove => SelectedEntry is not null && !IsBusy;

    /// <summary>
    /// Re-read the registry and repopulate <see cref="Layouts"/>.
    /// </summary>
    public void Refresh()
    {
        Layouts.Clear();
        foreach (var entry in _inspector.Inspect())
            Layouts.Add(entry);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
