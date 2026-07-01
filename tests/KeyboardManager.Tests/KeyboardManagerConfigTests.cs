using System.IO;
using System.Text.Json;
using KeyboardManager.Services.Configuration;

namespace KeyboardManager.Tests;

/// <summary>
/// Round-trip tests for <see cref="KeyboardManagerConfig"/> JSON serialisation —
/// the contract a user edits by hand when customising their reset target.
/// </summary>
public class KeyboardManagerConfigTests
{
    [Fact]
    public void RoundTrip_PreservesDefaultLayouts()
    {
        var original = new KeyboardManagerConfig
        {
            DefaultLayouts =
            {
                new DefaultLayout("00000409", "English (United States) — US"),
                new DefaultLayout("00000419", "Russian — Russian")
            }
        };

        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<KeyboardManagerConfig>(json);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.DefaultLayouts.Count);
        Assert.Equal("00000409", loaded.DefaultLayouts[0].Id);
        Assert.Equal("Russian — Russian", loaded.DefaultLayouts[1].Name);
    }

    /// <summary>
    /// The exact JSON format we ship in KeyboardManager.config.json must deserialise
    /// into the expected layout set — guards against accidental schema drift.
    /// </summary>
    [Fact]
    public void Deserialize_ShippedConfigFormat_ProducesUsAndRussian()
    {
        const string shippedJson = """
        {
          "DefaultLayouts": [
            { "Id": "00000409", "Name": "English (United States) — US" },
            { "Id": "00000419", "Name": "Russian — Russian" }
          ]
        }
        """;

        var loaded = JsonSerializer.Deserialize<KeyboardManagerConfig>(shippedJson);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.DefaultLayouts.Count);
        Assert.Equal("00000409", loaded.DefaultLayouts[0].Id);
        Assert.Equal("00000419", loaded.DefaultLayouts[1].Id);
    }

    /// <summary>
    /// A user customising to a single layout round-trips correctly.
    /// </summary>
    [Fact]
    public void RoundTrip_SingleLayout()
    {
        var original = new KeyboardManagerConfig
        {
            DefaultLayouts = { new DefaultLayout("00000409", "US only") }
        };

        var loaded = JsonSerializer.Deserialize<KeyboardManagerConfig>(
            JsonSerializer.Serialize(original));

        Assert.NotNull(loaded);
        Assert.Single(loaded!.DefaultLayouts);
        Assert.Equal("US only", loaded.DefaultLayouts[0].Name);
    }
}
