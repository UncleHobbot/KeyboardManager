using KeyboardManager.Services;

namespace KeyboardManager.Tests;

/// <summary>
/// In-memory <see cref="IKeyboardLayoutRegistry"/> for unit tests. Models the exact
/// ghost scenario observed on the developer's machine.
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
}
