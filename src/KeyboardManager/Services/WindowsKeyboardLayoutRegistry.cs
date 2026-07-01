using System.Globalization;
using Microsoft.Win32;

namespace KeyboardManager.Services;

/// <summary>
/// <see cref="IKeyboardLayoutRegistry"/> backed by the live Windows registry.
/// </summary>
public sealed class WindowsKeyboardLayoutRegistry : IKeyboardLayoutRegistry
{
    private const string PreloadSuffix = @"Keyboard Layout\Preload";
    private const string SubstitutesSuffix = @"Keyboard Layout\Substitutes";
    private const string LayoutsRoot = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    public IReadOnlyDictionary<string, string> GetPreloadValues(bool forDefaultHive)
    {
        var root = forDefaultHive ? Registry.Users : Registry.CurrentUser;
        // .DEFAULT under HKEY_USERS is the well-known S-1-5-18 SID.
        var subKey = forDefaultHive ? @".DEFAULT\" + PreloadSuffix : PreloadSuffix;
        return ReadStringValues(root, subKey);
    }

    public IReadOnlyDictionary<string, string> GetSubstituteValues(bool forDefaultHive)
    {
        var root = forDefaultHive ? Registry.Users : Registry.CurrentUser;
        var subKey = forDefaultHive ? @".DEFAULT\" + SubstitutesSuffix : SubstitutesSuffix;
        return ReadStringValues(root, subKey);
    }

    public string? GetLayoutText(string canonicalLayoutId)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"{LayoutsRoot}\{canonicalLayoutId}");
        return key?.GetValue("Layout Text") as string;
    }

    public string? GetLanguageName(string canonicalLayoutId)
    {
        // The low 4 hex digits of a canonical layout id are the language id (LCID low word).
        if (canonicalLayoutId.Length < 4)
            return null;

        var langHex = canonicalLayoutId[^4..];
        if (!int.TryParse(langHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lcid))
            return null;

        try
        {
            var culture = new CultureInfo(lcid);
            return culture.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            // Not every legacy layout id maps to a known culture (e.g. d-prefixed extended ids
            // whose canonical form still resolves here). Fall back to nothing.
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ReadStringValues(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path);
        if (key is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is string s && !string.IsNullOrWhiteSpace(s))
                result[name] = s;
        }

        return result;
    }

    public bool DeletePreloadValue(bool forDefaultHive, string valueName)
        => DeleteValue(forDefaultHive, PreloadSuffix, valueName);

    public bool DeleteSubstituteValue(bool forDefaultHive, string valueName)
        => DeleteValue(forDefaultHive, SubstitutesSuffix, valueName);

    public void ReplacePreloadValues(bool forDefaultHive, IReadOnlyDictionary<string, string> values)
    {
        var (root, subKey) = ResolveKey(forDefaultHive, PreloadSuffix);
        using var key = root.CreateSubKey(subKey, writable: true);

        foreach (var name in key.GetValueNames().ToArray())
            key.DeleteValue(name, throwOnMissingValue: false);

        foreach (var (name, val) in values)
            key.SetValue(name, val, RegistryValueKind.String);
    }

    public void ClearSubstitutes(bool forDefaultHive)
    {
        var (root, subKey) = ResolveKey(forDefaultHive, SubstitutesSuffix);
        using var key = root.OpenSubKey(subKey, writable: true);
        if (key is null) return;

        foreach (var name in key.GetValueNames().ToArray())
            key.DeleteValue(name, throwOnMissingValue: false);
    }

    private bool DeleteValue(bool forDefaultHive, string suffix, string valueName)
    {
        var (root, subKey) = ResolveKey(forDefaultHive, suffix);
        using var key = root.OpenSubKey(subKey, writable: true);
        if (key is null) return false;

        if (key.GetValue(valueName) is null) return false;
        key.DeleteValue(valueName, throwOnMissingValue: false);
        return true;
    }

    private static (RegistryKey Root, string SubKey) ResolveKey(bool forDefaultHive, string suffix)
    {
        return forDefaultHive
            ? (Registry.Users, @".DEFAULT\" + suffix)
            : (Registry.CurrentUser, suffix);
    }
}
