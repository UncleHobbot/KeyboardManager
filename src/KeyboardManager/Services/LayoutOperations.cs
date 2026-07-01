using KeyboardManager.Models;
using KeyboardManager.Services.Configuration;

namespace KeyboardManager.Services;

/// <summary>
/// Owns the three keyboard-layout operation flows — Remove, Reset, Backup — as
/// deep modules behind a narrow interface. Each method performs its full
/// sequence (backup → execute → best-effort apply → build result) and returns a
/// single <see cref="OperationResult"/>. The module never calls UI APIs, never
/// asks for confirmation, and never touches UI state; it is deterministic given
/// its inputs. See ADR-0002.
/// </summary>
public sealed class LayoutOperations
{
    private readonly BackupService _backup;
    private readonly LayoutRemovalService _removal;
    private readonly LayoutResetService _reset;
    private readonly ISessionLayoutApplier _applier;

    public LayoutOperations(
        BackupService backup,
        LayoutRemovalService removal,
        LayoutResetService reset,
        ISessionLayoutApplier applier)
    {
        _backup = backup;
        _removal = removal;
        _reset = reset;
        _applier = applier;
    }

    /// <summary>
    /// Remove a layout from every registry source it lives in. Takes a backup
    /// first, then plans + executes the removal, then best-effort unloads it from
    /// the running session. Caller must have already confirmed with the user.
    /// </summary>
    public OperationResult Remove(LayoutEntry entry)
    {
        var plan = _removal.PlanRemoval(entry);

        var backup = TakeBackup($"remove-{entry.LayoutId}");
        if (backup is null)
            return OperationResult.Failed(
                "Backup failed — nothing was changed.",
                new[] { "Backup failed before any registry write." });

        RemovalResult removal;
        try
        {
            removal = _removal.Execute(plan);
        }
        catch (Exception ex)
        {
            return OperationResult.Failed(
                $"Removal threw an exception. Backup: {backup.Path}",
                new[] { ex.Message },
                backupPath: backup.Path);
        }

        // Best-effort apply; failure here is not fatal (sign-out covers it).
        _applier.TryUnload(entry.LayoutId);

        var notes = new List<string>();
        if (backup.SkippedKeys.Count > 0)
            notes.Add($"{backup.SkippedKeys.Count} key(s) skipped during backup (likely .DEFAULT without elevation).");

        var success = removal.Errors.Count == 0;
        var summary = success
            ? $"Removed {removal.Applied}/{removal.Total} value(s) for '{entry.DisplayName}'."
            : $"Removed {removal.Applied}/{removal.Total} value(s) with errors.";

        return new OperationResult(
            Success: success,
            Summary: summary,
            BackupPath: backup.Path,
            NeedsSignOut: plan.NeedsElevation,
            ValuesChanged: removal.Applied,
            Errors: removal.Errors,
            Notes: notes);
    }

    /// <summary>
    /// Reset HKCU to the configured default set. Takes a backup first, clears
    /// HKCU Preload + Substitutes, writes the defaults, and broadcasts the change.
    /// <c>.DEFAULT</c> is never touched. Caller must have already confirmed.
    /// </summary>
    public OperationResult Reset(KeyboardManagerConfig config)
    {
        var target = LayoutResetService.DescribeTarget(config);

        var backup = TakeBackup("reset");
        if (backup is null)
            return OperationResult.Failed(
                "Backup failed — nothing was changed.",
                new[] { "Backup failed before any registry write." });

        try
        {
            _reset.Reset(config);
        }
        catch (Exception ex)
        {
            return OperationResult.Failed(
                $"Reset threw an exception. Backup: {backup.Path}",
                new[] { ex.Message },
                backupPath: backup.Path);
        }

        _applier.BroadcastSettingsChange();

        return OperationResult.Succeeded(
            summary: $"Reset HKCU to: {target}.",
            backupPath: backup.Path,
            needsSignOut: true,
            notes: new[] { $"Target: {target}" });
    }

    /// <summary>
    /// Take a manual backup of all four registry sources to a timestamped
    /// <c>.reg</c> file. No registry writes occur.
    /// </summary>
    public OperationResult Backup()
    {
        var backup = TakeBackup("manual");
        if (backup is null)
            return OperationResult.Failed(
                "Backup failed.",
                new[] { "Backup failed." });

        var notes = backup.SkippedKeys.Count > 0
            ? new[] { $"{backup.SkippedKeys.Count} key(s) skipped:\n" + string.Join("\n", backup.SkippedKeys) }
            : Array.Empty<string>();

        return OperationResult.Succeeded(
            summary: $"Backed up {backup.ExportedKeys.Count} key(s).",
            backupPath: backup.Path,
            notes: notes);
    }

    /// <summary>
    /// Take a backup, returning null on failure (so callers can short-circuit).
    /// </summary>
    private BackupResult? TakeBackup(string operation)
    {
        try
        {
            return _backup.BackupAll(operation);
        }
        catch
        {
            return null;
        }
    }
}
