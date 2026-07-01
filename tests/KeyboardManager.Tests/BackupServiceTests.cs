using System.IO;
using KeyboardManager.Services;

namespace KeyboardManager.Tests;

/// <summary>
/// Exercises <see cref="BackupService"/> against the live registry. These are
/// integration tests (they shell out to <c>reg.exe</c>), so they write to a temp
/// directory and assert on structure rather than specific layout ids.
/// </summary>
public class BackupServiceTests
{
    [Fact]
    public void BackupAll_WritesRegFileWithHeaderAndAtLeastHkcuPreload()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"km-backup-test-{Guid.NewGuid():N}");
        try
        {
            var svc = new BackupService(dir);
            var result = svc.BackupAll("test-run");

            Assert.True(File.Exists(result.Path));
            Assert.EndsWith("test-run.reg", Path.GetFileName(result.Path));

            var contents = File.ReadAllText(result.Path);
            Assert.Contains("Windows Registry Editor Version 5.00", contents);

            // HKCU Preload should be exportable without elevation, so it must be in
            // the exported set, not the skipped set.
            Assert.Contains(result.ExportedKeys,
                k => k.IndexOf("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase) >= 0
                  && k.IndexOf("Preload", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    [Fact]
    public void BackupAll_FileNameIsTimestampedAndSlugified()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"km-backup-name-{Guid.NewGuid():N}");
        try
        {
            var svc = new BackupService(dir);
            var result = svc.BackupAll("Remove ghosts!");

            var name = Path.GetFileName(result.Path);
            // Stamp is 8 digits + dash + 6 digits, then the slug.
            Assert.Matches(@"^\d{8}-\d{6}-remove-ghosts\.reg$", name);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }
    }
}
