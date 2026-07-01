namespace KeyboardManager.Models;

/// <summary>
/// The triage status of a resolved layout, as defined in CONTEXT.md.
/// </summary>
public enum LayoutStatus
{
    /// <summary>
    /// Active but not Declared — appears in the switcher, invisible in Settings.
    /// The core problem this tool exists to solve.
    /// </summary>
    Ghost,

    /// <summary>
    /// Reachable via the Settings "Remove" button — present in HKCU Preload.
    /// </summary>
    Declared,

    /// <summary>
    /// A Substitutes entry whose source id appears in no Preload key — a dangling
    /// remnant that can re-materialise a ghost.
    /// </summary>
    Orphan
}
