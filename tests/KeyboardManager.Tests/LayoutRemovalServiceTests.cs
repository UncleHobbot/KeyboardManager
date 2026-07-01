using KeyboardManager.Models;
using KeyboardManager.Services;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests <see cref="LayoutRemovalService.PlanRemoval"/> — now a pure projection
/// over a <see cref="ResolvedLayout"/> snapshot (no registry re-read). Execution
/// is exercised only for the HKCU-only path against the fake registry.
/// </summary>
public class LayoutRemovalServiceTests
{
    /// <summary>
    /// The canonical ghost: Ukrainian loaded from .DEFAULT Preload#4 via a
    /// d0010419 substitute. The plan must schedule the Preload slot AND the
    /// orphaned substitute entry, derived purely from the snapshot.
    /// </summary>
    [Fact]
    public void PlanRemoval_GhostFromDefault_IncludesPreloadAndOrphanedSubstitute()
    {
        var reg = new FakeKeyboardLayoutRegistry();
        var elevation = new FakeElevatedRunner();
        var svc = new LayoutRemovalService(reg, elevation);

        // The snapshot the resolver would have produced:
        var entry = new ResolvedLayout(
            LoadedLayoutId: "00000422",
            CanonicalLayoutId: "00000422",
            DisplayName: "Ukrainian",
            Status: LayoutStatus.Ghost,
            Sources: new[]
            {
                // The Preload slot holding d0010419
                new ResolvedSource(LayoutSourceKind.DefaultPreload, "4", "d0010419", "00000422", "00000422"),
                // The substitute entry that would orphan after removing the slot
                new ResolvedSource(LayoutSourceKind.DefaultSubstitutes, "d0010419", "d0010419", "00000422", "00000422")
            });

        var plan = svc.PlanRemoval(entry);

        Assert.True(plan.NeedsElevation);
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultPreload && t.ValueName == "4");
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultSubstitutes && t.ValueName == "d0010419");
    }

    /// <summary>
    /// A Declared layout only in HKCU: no elevation needed, local delete only.
    /// </summary>
    [Fact]
    public void PlanRemoval_HkcuOnlyLayout_DoesNotNeedElevation()
    {
        var reg = new FakeKeyboardLayoutRegistry();
        var svc = new LayoutRemovalService(reg, new FakeElevatedRunner());

        var entry = Res("00000419", LayoutStatus.Declared,
            new ResolvedSource(LayoutSourceKind.HkcuPreload, "1", "00000419", "00000419", null));

        var plan = svc.PlanRemoval(entry);

        Assert.False(plan.NeedsElevation);
        Assert.Empty(plan.ElevatedDeletes);
        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuPreload && t.ValueName == "1");
    }

    /// <summary>
    /// Executing a HKCU-only plan mutates the fake registry.
    /// </summary>
    [Fact]
    public void Execute_HkcuOnlyPlan_RemovesFromFakeRegistry()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            HkcuPreload = { ["1"] = "00000419" }
        };
        var svc = new LayoutRemovalService(reg, new FakeElevatedRunner());

        var entry = Res("00000419", LayoutStatus.Declared,
            new ResolvedSource(LayoutSourceKind.HkcuPreload, "1", "00000419", "00000419", null));

        var plan = svc.PlanRemoval(entry);
        var result = svc.Execute(plan);

        Assert.Equal(1, result.Applied);
        Assert.Empty(reg.HkcuPreload);
    }

    /// <summary>
    /// A layout loaded in both HKCU and .DEFAULT must be removed from both.
    /// </summary>
    [Fact]
    public void PlanRemoval_AcrossBothHives_SplitsLocalAndElevated()
    {
        var reg = new FakeKeyboardLayoutRegistry();
        var svc = new LayoutRemovalService(reg, new FakeElevatedRunner());

        var entry = Res("00000419", LayoutStatus.Declared,
            new ResolvedSource(LayoutSourceKind.HkcuPreload, "1", "00000419", "00000419", null),
            new ResolvedSource(LayoutSourceKind.DefaultPreload, "3", "00000419", "00000419", null));

        var plan = svc.PlanRemoval(entry);

        Assert.True(plan.NeedsElevation);
        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuPreload && t.ValueName == "1");
        Assert.Contains(plan.ElevatedDeletes, t => t.Kind == LayoutSourceKind.DefaultPreload && t.ValueName == "3");
    }

    /// <summary>
    /// Orphan cleanup derived purely from the snapshot: a Preload source whose
    /// RawLayoutId matches a substitute source's SlotName schedules that substitute
    /// for deletion — even when the substitute source wasn't explicitly in the entry.
    /// </summary>
    [Fact]
    public void PlanRemoval_PreloadSlotOrphansMatchingSubstitute_FromSnapshot()
    {
        var reg = new FakeKeyboardLayoutRegistry();
        var svc = new LayoutRemovalService(reg, new FakeElevatedRunner());

        // HKCU Preload slot 2 holds raw id 00000499, which is substituted to 00000422.
        // The snapshot carries both the Preload source and the Substitute source for the
        // same resolved layout.
        var entry = Res("00000422", LayoutStatus.Ghost,
            new ResolvedSource(LayoutSourceKind.HkcuPreload, "2", "00000499", "00000422", "00000422"),
            new ResolvedSource(LayoutSourceKind.HkcuSubstitutes, "00000499", "00000499", "00000422", "00000422"));

        var plan = svc.PlanRemoval(entry);

        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuPreload && t.ValueName == "2");
        Assert.Contains(plan.LocalDeletes, t => t.Kind == LayoutSourceKind.HkcuSubstitutes && t.ValueName == "00000499");
    }

    private static ResolvedLayout Res(string id, LayoutStatus status, params ResolvedSource[] sources)
        => new(id, id, id, status, sources);
}
