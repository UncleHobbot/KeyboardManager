namespace KeyboardManager.Models;

/// <summary>
/// The data returned by a keyboard-layout <em>operation</em> (Remove, Reset, or
/// Backup) — what was done, whether a backup was taken, whether a sign-out is
/// recommended, what errors occurred, and a human-readable summary.
/// </summary>
/// <remarks>
/// This is both the test surface for the operation flows (assert against it) and
/// the UI contract (the window renders from it). One DTO shared across all three
/// operations keeps the module's interface narrow — see ADR-0002.
/// </remarks>
/// <param name="Success">Whether the operation completed without exceptions.</param>
/// <param name="Summary">One-line human-readable summary for the status bar.</param>
/// <param name="BackupPath">Path to the <c>.reg</c> backup taken, or null if none.</param>
/// <param name="NeedsSignOut">True when a sign-out is recommended (e.g. <c>.DEFAULT</c> was touched).</param>
/// <param name="ValuesChanged">How many registry values were deleted/rewritten, or null when not applicable.</param>
/// <param name="Errors">Per-step error messages, if any.</param>
/// <param name="Notes">Additional notes for the user (skipped keys, target description, etc.).</param>
public sealed record OperationResult(
    bool Success,
    string Summary,
    string? BackupPath,
    bool NeedsSignOut,
    int? ValuesChanged,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Notes)
{
    /// <summary>
    /// Convenience factory for a successful operation with no complications.
    /// </summary>
    public static OperationResult Succeeded(string summary, string? backupPath = null,
        bool needsSignOut = false, int? valuesChanged = null, IReadOnlyList<string>? notes = null)
        => new(true, summary, backupPath, needsSignOut, valuesChanged,
            Array.Empty<string>(), notes ?? Array.Empty<string>());

    /// <summary>
    /// Convenience factory for a failed operation.
    /// </summary>
    public static OperationResult Failed(string summary, IReadOnlyList<string> errors,
        string? backupPath = null, IReadOnlyList<string>? notes = null)
        => new(false, summary, backupPath, NeedsSignOut: false, ValuesChanged: null,
            errors, notes ?? Array.Empty<string>());
}
