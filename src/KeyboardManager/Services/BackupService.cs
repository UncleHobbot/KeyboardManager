using System.Diagnostics;
using System.IO;
using System.Text;

namespace KeyboardManager.Services;

/// <summary>
/// Exports keyboard-layout registry keys to timestamped <c>.reg</c> files under
/// <c>./backups/</c>. Implements the first layer of the safety model: every
/// destructive operation must produce a restorable backup before touching the
/// registry.
/// </summary>
public sealed class BackupService
{
    private static readonly string[] SourceKeys =
    [
        @"HKEY_CURRENT_USER\Keyboard Layout\Preload",
        @"HKEY_CURRENT_USER\Keyboard Layout\Substitutes",
        @"HKEY_USERS\.DEFAULT\Keyboard Layout\Preload",
        @"HKEY_USERS\.DEFAULT\Keyboard Layout\Substitutes"
    ];

    private readonly string _backupDirectory;

    public BackupService(string? backupDirectory = null)
    {
        _backupDirectory = backupDirectory ?? Path.Combine(AppContext.BaseDirectory, "backups");
    }

    /// <summary>
    /// The directory backups are written to.
    /// </summary>
    public string BackupDirectory => _backupDirectory;

    /// <summary>
    /// Exports all known keyboard-layout sources to a single timestamped <c>.reg</c>
    /// file. Sources that fail to export (e.g. <c>.DEFAULT</c> keys under a
    /// non-elevated process) are skipped and recorded in <see cref="BackupResult.SkippedKeys"/>.
    /// </summary>
    /// <param name="operation">A short label folded into the file name.</param>
    /// <returns>The backup result, including the path and any skipped sources.</returns>
    public BackupResult BackupAll(string operation)
    {
        Directory.CreateDirectory(_backupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var slug = Sanitise(operation);
        var path = Path.Combine(_backupDirectory, $"{stamp}-{slug}.reg");

        var exported = new List<string>();
        var skipped = new List<string>();

        using (var writer = new StreamWriter(path, append: false))
        {
            writer.WriteLine("Windows Registry Editor Version 5.00");
            writer.WriteLine();

            foreach (var key in SourceKeys)
            {
                if (TryExportKey(key, out var text))
                {
                    writer.WriteLine(text);
                    exported.Add(key);
                }
                else
                {
                    skipped.Add(key);
                }
            }
        }

        return new BackupResult(path, exported, skipped);
    }

    /// <summary>
    /// Run <c>reg export</c> for a single key and return its body. Returns false if
    /// the key is absent or access is denied (common for <c>.DEFAULT</c> without
    /// elevation).
    /// </summary>
    private static bool TryExportKey(string keyPath, out string body)
    {
        body = string.Empty;

        var tempFile = Path.Combine(Path.GetTempPath(), $"km-{Guid.NewGuid():N}.reg");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"{keyPath}\" \"{tempFile}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit();
            if (proc.ExitCode != 0) return false;
            if (!File.Exists(tempFile)) return false;

            // Strip the "Windows Registry Editor Version 5.00" header from each
            // fragment so the merged file declares it once at the top.
            var raw = File.ReadAllText(tempFile);
            var headerEnd = raw.IndexOf('\n');
            body = headerEnd >= 0 ? raw[(headerEnd + 1)..] : raw;
            return !string.IsNullOrWhiteSpace(body);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { /* best effort */ }
            }
        }
    }

    private static string Sanitise(string operation)
    {
        var slug = new StringBuilder();
        foreach (var c in operation)
            slug.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        var result = slug.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "backup" : result;
    }

    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
}

/// <summary>
/// Result of a backup operation.
/// </summary>
/// <param name="Path">The <c>.reg</c> file written.</param>
/// <param name="ExportedKeys">Keys successfully exported.</param>
/// <param name="SkippedKeys">Keys that could not be exported (absent or access-denied).</param>
public sealed record BackupResult(string Path, IReadOnlyList<string> ExportedKeys, IReadOnlyList<string> SkippedKeys);
