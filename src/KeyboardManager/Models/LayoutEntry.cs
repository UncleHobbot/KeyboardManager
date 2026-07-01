namespace KeyboardManager.Models;

/// <summary>
/// One resolved keyboard layout as presented in the UI. Combines an identifier, a
/// human-readable name, a status, and every registry source that contributes to it
/// being loaded. See CONTEXT.md for the domain terms.
/// </summary>
public sealed record LayoutEntry(
    string LayoutId,
    string DisplayName,
    LayoutStatus Status,
    IReadOnlyList<LayoutSourceEntry> Sources)
{
    /// <summary>
    /// Convenience: the layout id without the optional leading substitute prefix
    /// (e.g. <c>d0010419</c> → <c>00000419</c>). Used for HKLM name lookups, which
    /// key on the canonical id.
    /// </summary>
    public string CanonicalId => LayoutId.Length == 8 && LayoutId[0] == 'd'
        ? "0000" + LayoutId[4..]
        : LayoutId;
}
