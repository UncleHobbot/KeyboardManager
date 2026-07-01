using KeyboardManager.Services;
using KeyboardManager.Services.Configuration;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests <see cref="LayoutResetService"/> against the fake registry. The fake
/// records mutations, so we can assert HKCU Preload/Substitutes end up exactly as
/// the default set prescribes.
/// </summary>
public class LayoutResetServiceTests
{
    [Fact]
    public void Reset_ClearsHkcuAndWritesDefaultSet()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload =
            {
                ["1"] = "00000409",
                ["2"] = "00001009",
                ["3"] = "d0010419"
            },
            HkcuSubstitutes = { ["d0010419"] = "00000422" }
        };

        var config = new KeyboardManagerConfig
        {
            DefaultLayouts =
            {
                new DefaultLayout("00000409", "US"),
                new DefaultLayout("00000419", "Russian")
            }
        };

        var svc = new LayoutResetService(reg, new SessionLayoutApplier());
        svc.Reset(config);

        Assert.Equal(2, reg.HkcuPreload.Count);
        Assert.Equal("00000409", reg.HkcuPreload["1"]);
        Assert.Equal("00000419", reg.HkcuPreload["2"]);
        Assert.Empty(reg.HkcuSubstitutes);
    }

    [Fact]
    public void Reset_DoesNotTouchDefaultHive()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["1"] = "00001009" },
            DefaultSubstitutes = { ["00001009"] = "00000409" }
        };

        var svc = new LayoutResetService(reg, new SessionLayoutApplier());
        svc.Reset(KeyboardManagerConfig.BuiltIn);

        // .DEFAULT must be untouched.
        Assert.Single(reg.DefaultPreload);
        Assert.Equal("00001009", reg.DefaultPreload["1"]);
        Assert.Single(reg.DefaultSubstitutes);
    }

    [Fact]
    public void DescribeTarget_FormatsNamesCommaSeparated()
    {
        var config = new KeyboardManagerConfig
        {
            DefaultLayouts =
            {
                new DefaultLayout("00000409", "English (US)"),
                new DefaultLayout("00000419", "Russian")
            }
        };

        Assert.Equal("English (US), Russian", LayoutResetService.DescribeTarget(config));
    }

    [Fact]
    public void Config_BuiltInDefaultIsUsAndRussian()
    {
        var builtIn = KeyboardManagerConfig.BuiltIn;

        Assert.Equal(2, builtIn.DefaultLayouts.Count);
        Assert.Equal("00000409", builtIn.DefaultLayouts[0].Id);
        Assert.Equal("00000419", builtIn.DefaultLayouts[1].Id);
    }
}
