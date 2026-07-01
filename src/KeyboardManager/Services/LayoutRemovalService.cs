using KeyboardManager.Models;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Services;

/// <summary>
/// Computes and executes the removal of a layout from every registry source it
/// lives in (FR-2). The plan is now a pure projection over a
/// <see cref="ResolvedLayout"/> snapshot — no registry re-read — and the execute
/// step applies it through <see cref="IKeyboardLayoutRegistry"/> and
/// <see cref="IElevatedOperationRunner"/>. See ADR-0003.
/// </summary>
public sealed class LayoutRemovalService
{
    private readonly IKeyboardLayoutRegistry _registry;
    private readonly IElevatedOperationRunner _elevation;

    public LayoutRemovalService(IKeyboardLayoutRegistry registry, IElevatedOperationRunner elevation)
    {
        _registry = registry;
        _elevation = elevation;
    }

    /// <summary>
    /// Build the removal plan for a resolved layout: every value to delete,
    /// grouped by privilege. Pure projection over the snapshot — no registry read.
    /// </summary>
    public RemovalPlan PlanRemoval(ResolvedLayout entry)
    {
        var localDeletes = new List<RemovalTarget>();
        var elevatedDeletes = new List<RemovalTarget>();

        foreach (var source in entry.Sources)
        {
            switch (source.Kind)
            {
                case LayoutSourceKind.HkcuPreload:
                    localDeletes.Add(new RemovalTarget(LayoutSourceKind.HkcuPreload, source.SlotName));
                    QueueOrphanedSubstitute(localDeletes, entry.Sources, source, LayoutSourceKind.HkcuSubstitutes);
                    break;
                case LayoutSourceKind.DefaultPreload:
                    elevatedDeletes.Add(new RemovalTarget(LayoutSourceKind.DefaultPreload, source.SlotName));
                    QueueOrphanedSubstitute(elevatedDeletes, entry.Sources, source, LayoutSourceKind.DefaultSubstitutes);
                    break;
                case LayoutSourceKind.HkcuSubstitutes:
                    localDeletes.Add(new RemovalTarget(LayoutSourceKind.HkcuSubstitutes, source.SlotName));
                    break;
                case LayoutSourceKind.DefaultSubstitutes:
                    elevatedDeletes.Add(new RemovalTarget(LayoutSourceKind.DefaultSubstitutes, source.SlotName));
                    break;
            }
        }

        return new RemovalPlan(entry, localDeletes, elevatedDeletes);
    }

    /// <summary>
    /// When a Preload source is being removed, any substitute entry keyed by its
    /// raw id would be orphaned — schedule it for deletion. Found by scanning the
    /// snapshot's substitute sources, no registry read.
    /// </summary>
    private static void QueueOrphanedSubstitute(
        List<RemovalTarget> deletes, IReadOnlyList<ResolvedSource> allSources,
        ResolvedSource removedPreload, LayoutSourceKind subKind)
    {
        foreach (var s in allSources)
        {
            if (s.Kind == subKind
                && string.Equals(s.SlotName, removedPreload.RawLayoutId, StringComparison.OrdinalIgnoreCase)
                && !deletes.Any(d => d.Kind == subKind && string.Equals(d.ValueName, s.SlotName, StringComparison.OrdinalIgnoreCase)))
            {
                deletes.Add(new RemovalTarget(subKind, s.SlotName));
            }
        }
    }

    /// <summary>
    /// Execute the plan. HKCU writes happen inline; .DEFAULT writes go through the
    /// elevation runner.
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
}

/// <summary>
/// One value to delete from one registry location.
/// </summary>
public sealed record RemovalTarget(LayoutSourceKind Kind, string ValueName);

/// <summary>
/// The computed plan for removing a layout, split by privilege.
/// </summary>
public sealed record RemovalPlan(
    ResolvedLayout Entry,
    IReadOnlyList<RemovalTarget> LocalDeletes,
    IReadOnlyList<RemovalTarget> ElevatedDeletes)
{
    public int TotalDeletes => LocalDeletes.Count + ElevatedDeletes.Count;
    public bool NeedsElevation => ElevatedDeletes.Count > 0;
}

/// <summary>
/// Outcome of executing a removal.
/// </summary>
public sealed record RemovalResult(int Applied, int Total, IReadOnlyList<string> Errors);
