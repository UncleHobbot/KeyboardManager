using KeyboardManager.Models;
using KeyboardManager.Services;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests for <see cref="LayoutResolver"/> — the single owner of the resolution
/// model. Ports the inspector scenarios, now asserting on <see cref="ResolvedLayout"/>
/// with its richer <see cref="ResolvedSource"/> fields (RawLayoutId, ViaSubstitute).
/// </summary>
public class LayoutResolverTests
{
    /// <summary>
    /// Reproduces the developer's machine: HKCU declares only Russian, but .DEFAULT
    /// carries three extra entries — two of them substitutes that load US (via
    /// Canadian French id 00001009) and Ukrainian (via d0010419).
    /// </summary>
    [Fact]
    public void Resolve_DeveloperMachineGhostScenario_FlagsUkrainianAndUsAsGhosts()
    {
        var reg = new FakeKeyboardLayoutRegistry();

        reg.HkcuPreload["1"] = "00000419";

        reg.DefaultPreload["1"] = "00001009";
        reg.DefaultPreload["2"] = "00000409";
        reg.DefaultPreload["3"] = "00000419";
        reg.DefaultPreload["4"] = "d0010419";

        reg.DefaultSubstitutes["00001009"] = "00000409";
        reg.DefaultSubstitutes["d0010419"] = "00000422";

        reg.LayoutTexts["00000409"] = "US";
        reg.LayoutTexts["00000419"] = "Russian";
        reg.LayoutTexts["00000422"] = "Ukrainian";
        reg.LanguageNames["00000409"] = "English (United States)";
        reg.LanguageNames["00000419"] = "Russian";
        reg.LanguageNames["00000422"] = "Ukrainian";

        var layouts = new LayoutResolver(reg).Resolve().Layouts;
        var byId = layouts.ToDictionary(l => l.LoadedLayoutId, StringComparer.OrdinalIgnoreCase);

        Assert.True(byId.ContainsKey("00000422"));
        Assert.Equal(LayoutStatus.Ghost, byId["00000422"].Status);

        Assert.True(byId.ContainsKey("00000409"));
        Assert.Equal(LayoutStatus.Ghost, byId["00000409"].Status);

        Assert.True(byId.ContainsKey("00000419"));
        Assert.Equal(LayoutStatus.Declared, byId["00000419"].Status);
    }

    /// <summary>
    /// The substitute mapping must be recorded on the source: a Preload slot with
    /// a substituted id carries ViaSubstitute set and RawLayoutId = the pre-substitute id.
    /// </summary>
    [Fact]
    public void Resolve_RecordsSubstituteMappingOnSource()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["4"] = "d0010419" },
            DefaultSubstitutes = { ["d0010419"] = "00000422" },
            LayoutTexts = { ["00000422"] = "Ukrainian" },
            LanguageNames = { ["00000422"] = "Ukrainian" }
        };

        var layouts = new LayoutResolver(reg).Resolve().Layouts;
        var ukrainian = Assert.Single(layouts);
        var source = Assert.Single(ukrainian.Sources);

        Assert.Equal("d0010419", source.RawLayoutId);
        Assert.Equal("00000422", source.LoadedLayoutId);
        Assert.Equal("00000422", source.ViaSubstitute);
    }

    /// <summary>
    /// The canonical id of a d-prefixed loaded id has the d stripped; the single
    /// place this transform lives.
    /// </summary>
    [Fact]
    public void Resolve_CanonicalIdStripsDPrefix()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["4"] = "d0010419" },
            DefaultSubstitutes = { ["d0010419"] = "00000422" }
        };

        var layouts = new LayoutResolver(reg).Resolve().Layouts;
        var ukrainian = Assert.Single(layouts);

        // Loaded id is the substitute target (00000422), already canonical.
        Assert.Equal("00000422", ukrainian.CanonicalLayoutId);
    }

    /// <summary>
    /// A substitute entry whose source id appears in no Preload is an Orphan.
    /// </summary>
    [Fact]
    public void Resolve_DanglingSubstituteIsOrphan()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000409" },
            HkcuSubstitutes = { ["00000499"] = "00000422" },
            LayoutTexts = { ["00000409"] = "US", ["00000422"] = "Ukrainian" },
            LanguageNames = { ["00000409"] = "English (United States)", ["00000422"] = "Ukrainian" }
        };

        var layouts = new LayoutResolver(reg).Resolve().Layouts;

        var orphan = Assert.Single(layouts, l => l.Status == LayoutStatus.Orphan);
        Assert.Equal("00000422", orphan.LoadedLayoutId);
        Assert.Contains(orphan.Sources, s => s.Kind == LayoutSourceKind.HkcuSubstitutes && s.SlotName == "00000499");
    }

    /// <summary>
    /// Russian appears exactly once even though present in both HKCU and .DEFAULT.
    /// </summary>
    [Fact]
    public void Resolve_MergesSameLayoutAcrossHives()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },
            DefaultPreload = { ["1"] = "00000419" },
            LayoutTexts = { ["00000419"] = "Russian" },
            LanguageNames = { ["00000419"] = "Russian" }
        };

        var layouts = new LayoutResolver(reg).Resolve().Layouts;

        var russian = Assert.Single(layouts);
        Assert.Equal("00000419", russian.LoadedLayoutId);
        Assert.Equal(2, russian.Sources.Count);
        Assert.Contains(russian.Sources, s => s.Kind == LayoutSourceKind.HkcuPreload);
        Assert.Contains(russian.Sources, s => s.Kind == LayoutSourceKind.DefaultPreload);
    }

    /// <summary>
    /// Ghosts sort before Declared, which sorts before Orphans.
    /// </summary>
    [Fact]
    public void Resolve_SortsGhostsFirstThenDeclaredThenOrphans()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },
            DefaultPreload = { ["1"] = "00000409" },
            HkcuSubstitutes = { ["00000499"] = "00000422" },
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

        var layouts = new LayoutResolver(reg).Resolve().Layouts;

        Assert.Equal(
            new[] { LayoutStatus.Ghost, LayoutStatus.Declared, LayoutStatus.Orphan },
            layouts.Select(l => l.Status).ToArray());
    }

    /// <summary>
    /// Unknown layout ids still render, falling back to the raw hex id.
    /// </summary>
    [Fact]
    public void Resolve_UnknownLayoutIdFallsBackToHexId()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "deadbeef" }
        };

        var layouts = new LayoutResolver(reg).Resolve().Layouts;

        var entry = Assert.Single(layouts);
        Assert.Equal("deadbeef", entry.LoadedLayoutId);
        Assert.Contains("deadbeef", entry.DisplayName);
    }
}
