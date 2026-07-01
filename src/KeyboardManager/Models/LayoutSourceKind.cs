namespace KeyboardManager.Models;

/// <summary>
/// Identifies one of the three registry locations from which a layout can be loaded.
/// Matches the "Layout source" glossary entry in CONTEXT.md.
/// </summary>
public enum LayoutSourceKind
{
    HkcuPreload,
    HkcuSubstitutes,
    DefaultPreload,
    DefaultSubstitutes
}
