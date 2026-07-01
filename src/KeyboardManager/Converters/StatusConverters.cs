using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KeyboardManager.Models;

namespace KeyboardManager.Converters;

/// <summary>
/// Maps a <see cref="LayoutStatus"/> to a badge background colour: red for ghosts
/// (the problem), green for declared (fine), amber for orphans (cleanup needed).
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly Brush Ghost = CreateBrush(0xC6, 0x28, 0x28);
    private static readonly Brush Declared = CreateBrush(0x2E, 0x7D, 0x32);
    private static readonly Brush Orphan = CreateBrush(0xEF, 0x6C, 0x00);

    private static Brush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LayoutStatus status)
            return Brushes.Gray;

        return status switch
        {
            LayoutStatus.Ghost => Ghost,
            LayoutStatus.Declared => Declared,
            LayoutStatus.Orphan => Orphan,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Maps a <see cref="LayoutStatus"/> to a short badge label.
/// </summary>
public sealed class StatusToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is LayoutStatus status ? status switch
        {
            LayoutStatus.Ghost => "👻 Ghost",
            LayoutStatus.Declared => "✓ Declared",
            LayoutStatus.Orphan => "🔶 Orphan",
            _ => "?"
        } : "?";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Renders the sources list as a comma-separated string of registry locations.
/// </summary>
public sealed class SourcesToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IReadOnlyList<LayoutSourceEntry> sources || sources.Count == 0)
            return "—";

        return string.Join(", ", sources.Select(SourceLabel));
    }

    private static string SourceLabel(LayoutSourceEntry s) => s.Kind switch
    {
        LayoutSourceKind.HkcuPreload => $"HKCU\\Preload#{s.ValueName}",
        LayoutSourceKind.HkcuSubstitutes => $"HKCU\\Substitutes#{s.ValueName}",
        LayoutSourceKind.DefaultPreload => $"HKU\\.DEFAULT\\Preload#{s.ValueName}",
        LayoutSourceKind.DefaultSubstitutes => $"HKU\\.DEFAULT\\Substitutes#{s.ValueName}",
        _ => s.ValueName
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
