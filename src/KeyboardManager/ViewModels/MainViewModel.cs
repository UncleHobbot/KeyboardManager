using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeyboardManager.Models;
using KeyboardManager.Services;

namespace KeyboardManager.ViewModels;

/// <summary>
/// View model for <see cref="MainWindow"/>. Holds the resolved layout list and
/// selection state. Operations themselves live in <see cref="LayoutOperations"/>
/// (ADR-0002); this VM only owns the read-side state.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LayoutResolver _resolver;
    private ResolvedLayout? _selectedEntry;
    private bool _isBusy;

    public MainViewModel(LayoutResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// The flat, ghost-first list of resolved layouts bound to the DataGrid.
    /// </summary>
    public ObservableCollection<ResolvedLayout> Layouts { get; } = new();

    public ResolvedLayout? SelectedEntry
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
    /// Re-read the registry via the resolver and repopulate <see cref="Layouts"/>.
    /// </summary>
    public void Refresh()
    {
        Layouts.Clear();
        foreach (var entry in _resolver.Resolve().Layouts)
            Layouts.Add(entry);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
