namespace KeyboardManager.Models;

/// <summary>
/// The complete resolved view of every keyboard layout loaded from the four
/// registry sources. Produced by <c>LayoutResolver.Resolve</c> and consumed
/// read-only by the UI and the operation layer. See CONTEXT.md "Resolved layout".
/// </summary>
public sealed record ResolvedLayoutSet(IReadOnlyList<ResolvedLayout> Layouts);

/// <summary>
/// One resolved keyboard layout — what is loaded, its canonical form, its
/// display name, its triage status, and the registry sources that feed it. This
/// single type serves both the UI (binding) and the operation/removal layer. See
/// ADR-0003.
/// </summary>
/// <param name="LoadedLayoutId">The layout id actually loaded (post-substitute).</param>
/// <param name="CanonicalLayoutId">The <c>d</c>-prefix-stripped form, for HKLM lookups.</param>
/// <param name="DisplayName">Human-readable name, ready to render.</param>
/// <param name="Status">Ghost / Declared / Orphan.</param>
/// <param name="Sources">Every registry location that contributes to loading this layout.</param>
public sealed record ResolvedLayout(
    string LoadedLayoutId,
    string CanonicalLayoutId,
    string DisplayName,
    LayoutStatus Status,
    IReadOnlyList<ResolvedSource> Sources);

/// <summary>
/// One registry location that feeds a resolved layout. Richer than the old
/// <c>LayoutSourceEntry</c>: carries the raw id held in the slot and (where
/// applicable) the substitute target, so removal can plan as a pure projection
/// without re-reading the registry.
/// </summary>
/// <param name="Kind">Which of the four registry sources.</param>
/// <param name="SlotName">Preload slot number ("1","2") or substitute key id.</param>
/// <param name="RawLayoutId">The id as written in the slot (pre-substitute).</param>
/// <param name="LoadedLayoutId">The id actually loaded from this slot (post-substitute).</param>
/// <param name="ViaSubstitute">The substitute target if remapped, null if direct.</param>
public sealed record ResolvedSource(
    LayoutSourceKind Kind,
    string SlotName,
    string RawLayoutId,
    string LoadedLayoutId,
    string? ViaSubstitute);
