using System.IO;
using KeyboardManager.Models;
using KeyboardManager.Services;
using KeyboardManager.Services.Configuration;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests for <see cref="LayoutOperations"/> — the three operation flows. These
/// exercise the orchestration (backup → execute → apply → result) against an
/// in-memory fake registry and canned fakes for the applier and elevation runner,
/// with no UI thread. This is the test surface ADR-0002 introduced.
/// </summary>
public class LayoutOperationsTests
{
    /// <summary>
    /// Build a <see cref="LayoutOperations"/> wired to fakes. The BackupService
    /// points at a fresh temp dir so it produces real .reg files.
    /// </summary>
    private static (LayoutOperations Ops, FakeKeyboardLayoutRegistry Reg, FakeSessionApplier Applier, FakeElevatedRunner Runner, string BackupDir)
        BuildSut()
    {
        var reg = new FakeKeyboardLayoutRegistry();
        var applier = new FakeSessionApplier();
        var runner = new FakeElevatedRunner();
        var backupDir = Path.Combine(Path.GetTempPath(), $"km-ops-test-{Guid.NewGuid():N}");
        var backup = new BackupService(backupDir);
        var removal = new LayoutRemovalService(reg, runner);
        var reset = new LayoutResetService(reg, applier);
        var ops = new LayoutOperations(backup, removal, reset, applier);
        return (ops, reg, applier, runner, backupDir);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Remove
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_GhostFromDefault_SucceedsAndFlagsSignOut()
    {
        var (ops, reg, _, runner, backupDir) = BuildSut();
        try
        {
            reg.DefaultPreload["4"] = "d0010419";
            reg.DefaultSubstitutes["d0010419"] = "00000422";
            runner.Result = new ElevatedResult(Applied: 2, Total: 2, Array.Empty<string>());

            var entry = MakeEntry("00000422", "Ukrainian", LayoutStatus.Ghost,
                new LayoutSourceEntry(LayoutSourceKind.DefaultPreload, "4"));

            var result = ops.Remove(entry);

            Assert.True(result.Success);
            Assert.True(result.NeedsSignOut); // touched .DEFAULT
            Assert.Equal(2, result.ValuesChanged);
            Assert.NotNull(result.BackupPath);
            Assert.Empty(result.Errors);
        }
        finally { CleanDir(backupDir); }
    }

    [Fact]
    public void Remove_HkcuOnlyGhost_NoSignOutNeeded()
    {
        var (ops, reg, _, _, backupDir) = BuildSut();
        try
        {
            reg.HkcuPreload["1"] = "00000409"; // only in HKCU → ghost if not declared via another path
            var entry = MakeEntry("00000409", "US", LayoutStatus.Declared,
                new LayoutSourceEntry(LayoutSourceKind.HkcuPreload, "1"));

            var result = ops.Remove(entry);

            Assert.True(result.Success);
            Assert.False(result.NeedsSignOut);
            Assert.Equal(1, result.ValuesChanged);
        }
        finally { CleanDir(backupDir); }
    }

    [Fact]
    public void Remove_ElevationDeclined_PropagatesError()
    {
        var (ops, reg, _, runner, backupDir) = BuildSut();
        try
        {
            reg.DefaultPreload["1"] = "00000409";
            runner.Result = new ElevatedResult(0, 1, new[] { "UAC declined (1223)." });

            var entry = MakeEntry("00000409", "US", LayoutStatus.Ghost,
                new LayoutSourceEntry(LayoutSourceKind.DefaultPreload, "1"));

            var result = ops.Remove(entry);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("UAC declined"));
        }
        finally { CleanDir(backupDir); }
    }

    [Fact]
    public void Remove_BestEffortAppliesUnload()
    {
        var (ops, reg, applier, runner, backupDir) = BuildSut();
        try
        {
            reg.HkcuPreload["1"] = "00000419";
            runner.Result = new ElevatedResult(0, 0, Array.Empty<string>());

            var entry = MakeEntry("00000419", "Russian", LayoutStatus.Declared,
                new LayoutSourceEntry(LayoutSourceKind.HkcuPreload, "1"));

            ops.Remove(entry);

            Assert.Contains("00000419", applier.UnloadedIds);
        }
        finally { CleanDir(backupDir); }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Reset
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsHkcuAndWritesDefaults()
    {
        var (ops, reg, applier, _, backupDir) = BuildSut();
        try
        {
            reg.HkcuPreload["1"] = "00000409";
            reg.HkcuPreload["2"] = "d0010419";
            reg.HkcuSubstitutes["d0010419"] = "00000422";

            var result = ops.Reset(KeyboardManagerConfig.BuiltIn);

            Assert.True(result.Success);
            Assert.True(result.NeedsSignOut);
            Assert.Equal(2, reg.HkcuPreload.Count);
            Assert.Equal("00000409", reg.HkcuPreload["1"]);
            Assert.Equal("00000419", reg.HkcuPreload["2"]);
            Assert.Empty(reg.HkcuSubstitutes);
            Assert.True(applier.BroadcastCount >= 1);
            Assert.Contains(result.Notes!, n => n.Contains("English"));
        }
        finally { CleanDir(backupDir); }
    }

    [Fact]
    public void Reset_DoesNotTouchDefaultHive()
    {
        var (ops, reg, _, _, backupDir) = BuildSut();
        try
        {
            reg.DefaultPreload["1"] = "00001009";

            ops.Reset(KeyboardManagerConfig.BuiltIn);

            // .DEFAULT untouched.
            Assert.Single(reg.DefaultPreload);
            Assert.Equal("00001009", reg.DefaultPreload["1"]);
        }
        finally { CleanDir(backupDir); }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Backup
    // ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Backup_ReturnsPathAndNoChanges()
    {
        var (ops, reg, _, _, backupDir) = BuildSut();
        try
        {
            reg.HkcuPreload["1"] = "00000419";

            var result = ops.Backup();

            Assert.True(result.Success);
            Assert.NotNull(result.BackupPath);
            Assert.Null(result.ValuesChanged);
            Assert.True(File.Exists(result.BackupPath));
            // Registry state unchanged.
            Assert.Single(reg.HkcuPreload);
        }
        finally { CleanDir(backupDir); }
    }

    // ───────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────

    private static LayoutEntry MakeEntry(string id, string name, LayoutStatus status, params LayoutSourceEntry[] sources)
        => new(id, name, status, sources);

    private static void CleanDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best effort */ }
    }
}
