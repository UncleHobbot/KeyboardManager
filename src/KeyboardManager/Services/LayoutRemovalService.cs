using KeyboardManager.Models;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Services;

/// <summary>
/// Computes and executes the removal of a layout from every registry source it
/// lives in (FR-2). Splitting the plan from the execution lets the UI show a
/// concrete confirmation dialog before anything is written.
/// </summary>
public sealed class LayoutRemovalService
{
    private readonly IKeyboardLayoutRegistry _registry;
    private readonly ElevatedOperationRunner _elevation;

    public LayoutRemovalService(IKeyboardLayoutRegistry registry, ElevatedOperationRunner elevation)
    {
        _registry = registry;
        _elevation = elevation;
    }

    /// <summary>
    /// Build the removal plan for a single selected layout: every value to delete,
    /// grouped by whether it needs elevation (anything under .DEFAULT).
    /// </summary>
    public RemovalPlan PlanRemoval(LayoutEntry entry)
    {
        var localDeletes = new List<RemovalTarget>();
        var elevatedDeletes = new List<RemovalTarget>();

        // We need to re-read the live state to map back from source entries to the
        // raw Preload/substitute values. A source entry's ValueName is exactly the
        // registry value name to delete, but substitute entries are keyed by the
        // source id, and a Preload slot holding a substituted id must also have its
        // matching substitute entry cleaned up to avoid orphans.
        var hkcuPreload = _registry.GetPreloadValues(forDefaultHive: false);
        var defaultPreload = _registry.GetPreloadValues(forDefaultHive: true);

        foreach (var source in entry.Sources)
        {
            switch (source.Kind)
            {
                case LayoutSourceKind.HkcuPreload:
                    localDeletes.Add(new RemovalTarget(LayoutSourceKind.HkcuPreload, source.ValueName));
                    break;
                case LayoutSourceKind.DefaultPreload:
                    elevatedDeletes.Add(new RemovalTarget(LayoutSourceKind.DefaultPreload, source.ValueName));
                    break;
                case LayoutSourceKind.HkcuSubstitutes:
                    localDeletes.Add(new RemovalTarget(LayoutSourceKind.HkcuSubstitutes, source.ValueName));
                    break;
                case LayoutSourceKind.DefaultSubstitutes:
                    elevatedDeletes.Add(new RemovalTarget(LayoutSourceKind.DefaultSubstitutes, source.ValueName));
                    break;
            }
        }

        // After removing Preload slots, any substitute entry keyed by those slots'
        // raw ids becomes a dangling orphan — clean those up too.
        AddOrphanedSubstitutes(localDeletes, elevatedDeletes, hkcuPreload, defaultPreload, entry);

        return new RemovalPlan(entry, localDeletes, elevatedDeletes);
    }

    /// <summary>
    /// Execute the plan. HKCU writes happen inline; .DEFAULT writes go through the
    /// elevation runner. Returns the combined result.
    /// </summary>
    public RemovalResult Execute(RemovalPlan plan)
    {
        var applied = 0;
        var errors = new List<string>();

        foreach (var target in plan.LocalDeletes)
        {
            try
            {
                if (ApplyLocal(target)) applied++;
            }
            catch (Exception ex)
            {
                errors.Add($"{target.Kind}#{target.ValueName}: {ex.Message}");
            }
        }

        if (plan.ElevatedDeletes.Count > 0)
        {
            var ops = plan.ElevatedDeletes.Select(t => new ElevatedOperation(
                t.Kind == LayoutSourceKind.DefaultPreload
                    ? ElevatedKeyKind.DefaultPreload
                    : ElevatedKeyKind.DefaultSubstitutes,
                t.ValueName)).ToList();

            var result = _elevation.Run(ops);
            applied += result.Applied;
            errors.AddRange(result.Errors);
        }

        return new RemovalResult(applied, plan.TotalDeletes, errors);
    }

    private bool ApplyLocal(RemovalTarget target)
    {
        return target.Kind switch
        {
            LayoutSourceKind.HkcuPreload => _registry.DeletePreloadValue(false, target.ValueName),
            LayoutSourceKind.HkcuSubstitutes => _registry.DeleteSubstituteValue(false, target.ValueName),
            _ => false
        };
    }

    /// <summary>
    /// Find substitute entries whose source id equals a Preload slot we're about to
    /// remove, and queue them for deletion too (otherwise they become orphans).
    /// </summary>
    private static void AddOrphanedSubstitutes(
        List<RemovalTarget> local, List<RemovalTarget> elevated,
        IReadOnlyDictionary<string, string> hkcuPreload, IReadOnlyDictionary<string, string> defaultPreload,
        LayoutEntry entry)
    {
        // For each removed Preload slot, look up its raw id; if that raw id is a key
        // in the matching Substitutes map, schedule that substitute entry for removal.
        foreach (var target in local.Where(t => t.Kind == LayoutSourceKind.HkcuPreload).ToList())
        {
            if (hkcuPreload.TryGetValue(target.ValueName, out var rawId))
            {
                if (!local.Any(t => t.Kind == LayoutSourceKind.HkcuSubstitutes && t.ValueName == rawId))
                    local.Add(new RemovalTarget(LayoutSourceKind.HkcuSubstitutes, rawId));
            }
        }

        foreach (var target in elevated.Where(t => t.Kind == LayoutSourceKind.DefaultPreload).ToList())
        {
            if (defaultPreload.TryGetValue(target.ValueName, out var rawId))
            {
                if (!elevated.Any(t => t.Kind == LayoutSourceKind.DefaultSubstitutes && t.ValueName == rawId))
                    elevated.Add(new RemovalTarget(LayoutSourceKind.DefaultSubstitutes, rawId));
            }
        }
    }
}

/// <summary>
/// One value to delete from one registry location.
/// </summary>
public sealed record RemovalTarget(LayoutSourceKind Kind, string ValueName);

/// <summary>
/// The computed plan for removing a layout, split by privilege.
/// </summary>
public sealed record RemovalPlan(
    LayoutEntry Entry,
    IReadOnlyList<RemovalTarget> LocalDeletes,
    IReadOnlyList<RemovalTarget> ElevatedDeletes)
{
    /// <summary>Total number of values to delete across both groups.</summary>
    public int TotalDeletes => LocalDeletes.Count + ElevatedDeletes.Count;

    /// <summary>True if any delete targets .DEFAULT (needs UAC).</summary>
    public bool NeedsElevation => ElevatedDeletes.Count > 0;
}

/// <summary>
/// Outcome of executing a removal.
/// </summary>
public sealed record RemovalResult(int Applied, int Total, IReadOnlyList<string> Errors);
