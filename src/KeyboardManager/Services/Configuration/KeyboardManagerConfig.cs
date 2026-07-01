using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardManager.Services.Configuration;

/// <summary>
/// The persisted configuration for the Reset operation (FR-4). Defines the
/// known-good default set of layouts that Reset restores.
/// </summary>
public sealed class KeyboardManagerConfig
{
    /// <summary>
    /// The layouts to write into HKCU Preload on Reset, in slot order.
    /// </summary>
    public List<DefaultLayout> DefaultLayouts { get; set; } = new();

    /// <summary>
    /// The built-in default: English (US) + Russian. Used when no config file exists
    /// or the file is malformed.
    /// </summary>
    public static KeyboardManagerConfig BuiltIn => new()
    {
        DefaultLayouts =
        [
            new DefaultLayout("00000409", "English (United States) — US"),
            new DefaultLayout("00000419", "Russian — Russian")
        ]
    };

    /// <summary>
    /// Load from <c>KeyboardManager.config.json</c> next to the exe, falling back to
    /// <c>%APPDATA%\KeyboardManager\config.json</c>, then to <see cref="BuiltIn"/>.
    /// Never throws — a malformed file yields the built-in default.
    /// </summary>
    public static KeyboardManagerConfig Load()
    {
        foreach (var path in CandidatePaths())
        {
            if (TryRead(path, out var cfg))
            {
                cfg.SourcePath = path;
                return cfg;
            }
        }

        var builtIn = BuiltIn;
        builtIn.SourcePath = null;
        return builtIn;
    }

    /// <summary>
    /// The file this config was loaded from, or null if the built-in default is in
    /// use because no file was found.
    /// </summary>
    [JsonIgnore]
    public string? SourcePath { get; private set; }

    private static IEnumerable<string> CandidatePaths()
    {
        var exeDir = AppContext.BaseDirectory;
        yield return Path.Combine(exeDir, "KeyboardManager.config.json");

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
            yield return Path.Combine(appData, "KeyboardManager", "config.json");
    }

    private static bool TryRead(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out KeyboardManagerConfig? config)
    {
        config = null;
        try
        {
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            config = JsonSerializer.Deserialize<KeyboardManagerConfig>(json);
            return config is not null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// One layout in the default set. <see cref="Id"/> is the registry layout id;
/// <see cref="Name"/> is purely informational (shown in the UI next to Reset).
/// </summary>
public sealed record DefaultLayout(string Id, string Name);
