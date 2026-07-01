using KeyboardManager.Models;
using KeyboardManager.Services;

namespace KeyboardManager.Tests;

public class LayoutInspectorTests
{
    /// <summary>
    /// Reproduces the developer's machine: HKCU declares only Russian, but .DEFAULT
    /// carries three extra entries — two of them substitutes that load US (via
    /// Canadian French id 00001009) and Ukrainian (via d0010419). The Russian entry
    /// in .DEFAULT must merge with the HKCU one, not duplicate.
    /// </summary>
    [Fact]
    public void Inspect_DeveloperMachineGhostScenario_FlagsUkrainianAndUsAsGhosts()
    {
        var reg = new FakeKeyboardLayoutRegistry();

        // HKCU Preload: only Russian declared (this is all Settings shows).
        reg.HkcuPreload["1"] = "00000419";

        // .DEFAULT Preload: the full ghost set.
        reg.DefaultPreload["1"] = "00001009"; // Canadian French id, substituted below
        reg.DefaultPreload["2"] = "00000409"; // US
        reg.DefaultPreload["3"] = "00000419"; // Russian (also declared in HKCU)
        reg.DefaultPreload["4"] = "d0010419"; // Russian-prefixed id, substituted to Ukrainian

        // .DEFAULT Substitutes: the secret remaps.
        reg.DefaultSubstitutes["00001009"] = "00000409"; // Canadian French → US
        reg.DefaultSubstitutes["d0010419"] = "00000422"; // extended Russian → Ukrainian

        // HKLM name lookups.
        reg.LayoutTexts["00000409"] = "US";
        reg.LayoutTexts["00000419"] = "Russian";
        reg.LayoutTexts["00000422"] = "Ukrainian";
        reg.LanguageNames["00000409"] = "English (United States)";
        reg.LanguageNames["00000419"] = "Russian";
        reg.LanguageNames["00000422"] = "Ukrainian";

        var inspector = new LayoutInspector(reg);
        var entries = inspector.Inspect();

        // Expected resolved layouts, ghost-first:
        //   Ghost: Ukrainian (00000422) — from .DEFAULT only, via d0010419 substitute
        //   Ghost: US (00000409) — from .DEFAULT only (via 00001009 substitute AND direct)
        //   Declared: Russian (00000419) — in HKCU Preload (and .DEFAULT)

        var byId = entries.ToDictionary(e => e.LayoutId, StringComparer.OrdinalIgnoreCase);

        Assert.True(byId.ContainsKey("00000422"));
        Assert.Equal(LayoutStatus.Ghost, byId["00000422"].Status);

        Assert.True(byId.ContainsKey("00000409"));
        Assert.Equal(LayoutStatus.Ghost, byId["00000409"].Status);

        Assert.True(byId.ContainsKey("00000419"));
        Assert.Equal(LayoutStatus.Declared, byId["00000419"].Status);
    }

    /// <summary>
    /// A substitute entry whose source id appears in no Preload key is an Orphan —
    /// a dangling remnant. It must surface with the Orphan status.
    /// </summary>
    [Fact]
    public void Inspect_DanglingSubstituteIsOrphan()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000409" },
            HkcuSubstitutes = { ["00000499"] = "00000422" }, // 00000499 is in no Preload
            LayoutTexts = { ["00000409"] = "US", ["00000422"] = "Ukrainian" },
            LanguageNames = { ["00000409"] = "English (United States)", ["00000422"] = "Ukrainian" }
        };

        var entries = new LayoutInspector(reg).Inspect();

        var orphan = Assert.Single(entries, e => e.Status == LayoutStatus.Orphan);
        Assert.Equal("00000422", orphan.LayoutId);
        Assert.Contains(orphan.Sources, s => s.Kind == LayoutSourceKind.HkcuSubstitutes && s.ValueName == "00000499");
    }

    /// <summary>
    /// Russian must appear exactly once even though it is present in both HKCU and
    /// .DEFAULT Preload — sources merge into a single entry.
    /// </summary>
    [Fact]
    public void Inspect_MergesSameLayoutAcrossHives()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },
            DefaultPreload = { ["1"] = "00000419" },
            LayoutTexts = { ["00000419"] = "Russian" },
            LanguageNames = { ["00000419"] = "Russian" }
        };

        var entries = new LayoutInspector(reg).Inspect();

        var russian = Assert.Single(entries);
        Assert.Equal("00000419", russian.LayoutId);
        Assert.Equal(2, russian.Sources.Count);
        Assert.Contains(russian.Sources, s => s.Kind == LayoutSourceKind.HkcuPreload);
        Assert.Contains(russian.Sources, s => s.Kind == LayoutSourceKind.DefaultPreload);
    }

    /// <summary>
    /// Ghosts sort before Declared, which sorts before Orphans, per FR-1.
    /// </summary>
    [Fact]
    public void Inspect_SortsGhostsFirstThenDeclaredThenOrphans()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },                            // declared
            DefaultPreload = { ["1"] = "00000409" },                          // ghost
            HkcuSubstitutes = { ["00000499"] = "00000422" },                  // orphan
            LayoutTexts =
            {
                ["00000409"] = "US",
                ["00000419"] = "Russian",
                ["00000422"] = "Ukrainian"
            },
            LanguageNames =
            {
                ["00000409"] = "English (United States)",
                ["00000419"] = "Russian",
                ["00000422"] = "Ukrainian"
            }
        };

        var entries = new LayoutInspector(reg).Inspect();

        Assert.Equal(
            new[] { LayoutStatus.Ghost, LayoutStatus.Declared, LayoutStatus.Orphan },
            entries.Select(e => e.Status).ToArray());
    }

    /// <summary>
    /// Unknown layout ids (no HKLM entry) must still render, falling back to the
    /// raw hex id rather than throwing.
    /// </summary>
    [Fact]
    public void Inspect_UnknownLayoutIdFallsBackToHexId()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "deadbeef" }
        };

        var entries = new LayoutInspector(reg).Inspect();

        var entry = Assert.Single(entries);
        Assert.Equal("deadbeef", entry.LayoutId);
        Assert.Contains("deadbeef", entry.DisplayName);
    }
}
