using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace KeyboardManager.Services.Elevation;

/// <summary>
/// The non-elevated side of the elevate-on-demand handoff (ADR-0001). Writes the
/// operation list to a temp file, relaunches the current executable with
/// <c>runas</c> and the <c>--elevated</c> flag, then reads back the result.
///
/// <para>
/// If the user declines UAC, this returns a result with <see cref="ElevatedResult.Errors"/>
/// containing a single "declined" entry rather than throwing.
/// </para>
/// </summary>
public sealed class ElevatedOperationRunner : IElevatedOperationRunner
{
    /// <summary>
    /// The command-line flag that activates helper mode inside the same exe.
    /// </summary>
    public const string HelperFlag = "--elevated";

    private readonly string _exePath;

    public ElevatedOperationRunner(string? exePath = null)
    {
        _exePath = exePath ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the current executable path.");
    }

    /// <summary>
    /// Elevate and apply the operations. Returns the helper's result, or a result
    /// with a "declined" error if the user cancelled UAC.
    /// </summary>
    public ElevatedResult Run(IReadOnlyList<ElevatedOperation> operations)
    {
        var opsFile = Path.Combine(Path.GetTempPath(), $"km-ops-{Guid.NewGuid():N}.json");
        var resultFile = Path.Combine(Path.GetTempPath(), $"km-result-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(opsFile,
                JsonSerializer.Serialize(operations.ToArray(), ElevatedOperationJsonContext.Default.ElevatedOperationArray));

            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                UseShellExecute = true, // required for Verb = "runas"
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add(HelperFlag);
            psi.ArgumentList.Add("--ops");
            psi.ArgumentList.Add(opsFile);
            psi.ArgumentList.Add("--result");
            psi.ArgumentList.Add(resultFile);
            psi.Verb = "runas";

            Process? proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223 is ERROR_CANCELLED — the user clicked No on the UAC prompt.
                return new ElevatedResult(0, operations.Count,
                    new[] { $"UAC declined ({ex.NativeErrorCode})." });
            }

            if (proc is null)
                return new ElevatedResult(0, operations.Count, new[] { "Failed to start elevated process." });

            proc.WaitForExit();

            if (!File.Exists(resultFile))
                return new ElevatedResult(0, operations.Count,
                    new[] { $"Elevated helper did not write a result (exit {proc.ExitCode})." });

            var json = File.ReadAllText(resultFile);
            return JsonSerializer.Deserialize(json, ElevatedOperationJsonContext.Default.ElevatedResult)
                ?? new ElevatedResult(0, operations.Count, new[] { "Empty result." });
        }
        finally
        {
            TryDelete(opsFile);
            TryDelete(resultFile);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
