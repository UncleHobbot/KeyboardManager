using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using KeyboardManager.Services;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager;

/// <summary>
/// Application entry point. When launched with <c>--elevated</c>, runs the elevated
/// helper instead of showing the UI (ADR-0001). Otherwise boots the main window.
/// </summary>
public partial class App : Application
{
    private const string OpsArg = "--ops";
    private const string ResultArg = "--result";

    protected override void OnStartup(StartupEventArgs e)
    {
        var args = e.Args;

        if (args.Length > 0 && args[0] == ElevatedOperationRunner.HelperFlag)
        {
            RunElevatedHelper(args);
            Shutdown(exitCode: 0);
            return;
        }

        base.OnStartup(e);
    }

    /// <summary>
    /// Read the ops file, apply via <see cref="ElevatedHelper"/>, write the result
    /// file. Exits immediately — no UI.
    /// </summary>
    private static void RunElevatedHelper(string[] args)
    {
        var opsPath = ArgValue(args, OpsArg);
        var resultPath = ArgValue(args, ResultArg);
        if (opsPath is null || resultPath is null)
            return;

        if (!File.Exists(opsPath))
        {
            ElevatedHelper.WriteResult(
                new ElevatedResult(0, 0, new[] { "Operations file not found." }),
                resultPath);
            return;
        }

        var json = File.ReadAllText(opsPath);
        var ops = JsonSerializer.Deserialize(json, ElevatedOperationJsonContext.Default.ElevatedOperationArray)
                  ?? Array.Empty<ElevatedOperation>();

        var registry = new WindowsKeyboardLayoutRegistry();
        var helper = new ElevatedHelper(registry);
        var result = helper.Run(ops);
        ElevatedHelper.WriteResult(result, resultPath);
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
    }
}
