using KeyboardManager.Services;

namespace KeyboardManager.Tests;

/// <summary>
/// In-memory <see cref="IKeyboardLayoutRegistry"/> for unit tests. Both reads and
/// writes operate on the same dictionaries, so tests can assert on the resulting
/// state after a mutation.
/// </summary>
internal sealed class FakeKeyboardLayoutRegistry : IKeyboardLayoutRegistry
{
    public Dictionary<string, string> HkcuPreload { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> HkcuSubstitutes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DefaultPreload { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DefaultSubstitutes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LayoutTexts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> LanguageNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> GetPreloadValues(bool forDefaultHive)
        => forDefaultHive ? DefaultPreload : HkcuPreload;

    public IReadOnlyDictionary<string, string> GetSubstituteValues(bool forDefaultHive)
        => forDefaultHive ? DefaultSubstitutes : HkcuSubstitutes;

    public string? GetLayoutText(string canonicalLayoutId)
        => LayoutTexts.TryGetValue(canonicalLayoutId, out var v) ? v : null;

    public string? GetLanguageName(string canonicalLayoutId)
        => LanguageNames.TryGetValue(canonicalLayoutId, out var v) ? v : null;

    public bool DeletePreloadValue(bool forDefaultHive, string valueName)
        => PickPreload(forDefaultHive).Remove(valueName);

    public bool DeleteSubstituteValue(bool forDefaultHive, string valueName)
        => PickSubstitutes(forDefaultHive).Remove(valueName);

    public void ReplacePreloadValues(bool forDefaultHive, IReadOnlyDictionary<string, string> values)
    {
        var dict = PickPreload(forDefaultHive);
        dict.Clear();
        foreach (var (k, v) in values) dict[k] = v;
    }

    public void ClearSubstitutes(bool forDefaultHive)
        => PickSubstitutes(forDefaultHive).Clear();

    private Dictionary<string, string> PickPreload(bool forDefaultHive)
        => forDefaultHive ? DefaultPreload : HkcuPreload;

    private Dictionary<string, string> PickSubstitutes(bool forDefaultHive)
        => forDefaultHive ? DefaultSubstitutes : HkcuSubstitutes;
}
