using KeyboardManager.Models;
using KeyboardManager.Services;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests <see cref="LayoutRemovalService.PlanRemoval"/> — the planning logic that
/// decides what gets deleted. Execution (which shells out / elevates) is exercised
/// only for the HKCU-only path here, against the fake registry.
/// </summary>
public class LayoutRemovalServiceTests
{
    /// <summary>
    /// The canonical ghost on the developer's machine: Ukrainian is loaded via the
    /// d0010419 substitute in .DEFAULT. Planning it must produce one elevated delete
    /// for the .DEFAULT Preload slot AND the now-orphaned substitute entry.
    /// </summary>
    [Fact]
    public void PlanRemoval_GhostFromDefault_IncludesPreloadAndOrphanedSubstitute()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },
            DefaultPreload = { ["4"] = "d0010419" },
            DefaultSubstitutes = { ["d0010419"] = "00000422" }
        };

        // Build a synthetic entry as the inspector would: Ukrainian loaded from
        // .DEFAULT Preload#4 via the substitute.
        var entry = new LayoutEntry("00000422", "Ukrainian — Ukrainian (00000422)",
            LayoutStatus.Ghost,
            new[] { new LayoutSourceEntry(LayoutSourceKind.DefaultPreload, "4") });

        var elevation = new ElevatedOperationRunner("dummy.exe");
        var svc = new LayoutRemovalService(reg, elevation);
        var plan = svc.PlanRemoval(entry);

        Assert.True(plan.NeedsElevation);
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultPreload && t.ValueName == "4");
        // The substitute keyed by the removed Preload slot's raw id (d0010419) is orphaned.
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultSubstitutes && t.ValueName == "d0010419");
    }

    /// <summary>
    /// A Declared layout only in HKCU: no elevation needed, local delete only.
    /// </summary>
    [Fact]
    public void PlanRemoval_HkcuOnlyLayout_DoesNotNeedElevation()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" }
        };

        var entry = new LayoutEntry("00000419", "Russian",
            LayoutStatus.Declared,
            new[] { new LayoutSourceEntry(LayoutSourceKind.HkcuPreload, "1") });

        var svc = new LayoutRemovalService(reg, new ElevatedOperationRunner("dummy.exe"));
        var plan = svc.PlanRemoval(entry);

        Assert.False(plan.NeedsElevation);
        Assert.Empty(plan.ElevatedDeletes);
        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuPreload && t.ValueName == "1");
    }

    /// <summary>
    /// Executing a HKCU-only plan actually mutates the fake registry.
    /// </summary>
    [Fact]
    public void Execute_HkcuOnlyPlan_RemovesFromFakeRegistry()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" }
        };

        var entry = new LayoutEntry("00000419", "Russian",
            LayoutStatus.Declared,
            new[] { new LayoutSourceEntry(LayoutSourceKind.HkcuPreload, "1") });

        var svc = new LayoutRemovalService(reg, new ElevatedOperationRunner("dummy.exe"));
        var plan = svc.PlanRemoval(entry);
        var result = svc.Execute(plan);

        Assert.Equal(1, result.Applied);
        Assert.Empty(reg.HkcuPreload);
    }

    /// <summary>
    /// A layout loaded in both HKCU and .DEFAULT must be removed from both, and the
    /// plan must need elevation.
    /// </summary>
    [Fact]
    public void PlanRemoval_AcrossBothHives_SplitsLocalAndElevated()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" },
            DefaultPreload = { ["3"] = "00000419" }
        };

        var entry = new LayoutEntry("00000419", "Russian",
            LayoutStatus.Declared,
            new[]
            {
                new LayoutSourceEntry(LayoutSourceKind.HkcuPreload, "1"),
                new LayoutSourceEntry(LayoutSourceKind.DefaultPreload, "3")
            });

        var svc = new LayoutRemovalService(reg, new ElevatedOperationRunner("dummy.exe"));
        var plan = svc.PlanRemoval(entry);

        Assert.True(plan.NeedsElevation);
        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuPreload && t.ValueName == "1");
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultPreload && t.ValueName == "3");
    }
}
