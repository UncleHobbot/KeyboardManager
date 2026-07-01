using KeyboardManager.Services.Configuration;

namespace KeyboardManager.Services;

/// <summary>
/// Implements FR-3 — restore the HKCU input configuration to a known-good default
/// set. Clears HKCU Preload + Substitutes and writes the configured defaults.
///
/// <para>
/// Per the design, <c>.DEFAULT</c> is <b>never</b> touched by Reset — that hive
/// governs the logon/welcome screen and is a separate concern handled only by
/// targeted removal (issue 05).
/// </para>
/// </summary>
public sealed class LayoutResetService
{
    private readonly IKeyboardLayoutRegistry _registry;
    private readonly SessionLayoutApplier _applier;

    public LayoutResetService(IKeyboardLayoutRegistry registry, SessionLayoutApplier applier)
    {
        _registry = registry;
        _applier = applier;
    }

    /// <summary>
    /// Reset HKCU to the configured default set. The caller is responsible for
    /// taking a backup first (BackupService) and confirming with the user.
    /// </summary>
    public void Reset(KeyboardManagerConfig config)
    {
        var preload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slot = 1;
        foreach (var layout in config.DefaultLayouts)
        {
            preload[slot.ToString()] = layout.Id;
            slot++;
        }

        _registry.ReplacePreloadValues(forDefaultHive: false, preload);
        _registry.ClearSubstitutes(forDefaultHive: false);

        // Best-effort notify the shell.
        _applier.BroadcastSettingsChange();
    }

    /// <summary>
    /// A human-readable summary of what Reset will produce, for the confirmation
    /// dialog. e.g. "English (US), Russian".
    /// </summary>
    public static string DescribeTarget(KeyboardManagerConfig config)
        => config.DefaultLayouts.Count == 0
            ? "(empty)"
            : string.Join(", ", config.DefaultLayouts.Select(l => l.Name));
}
