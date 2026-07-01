using KeyboardManager.Models;

namespace KeyboardManager.Services;

/// <summary>
/// Produces the unified view of keyboard layouts for FR-1. Reads the four registry
/// sources via <see cref="IKeyboardLayoutRegistry"/>, resolves substitutes, joins
/// the results, and classifies each layout as Ghost / Declared / Orphan.
///
/// <para>
/// <b>Resolution model.</B> A Preload slot holds a layout id. If that id also
/// appears as a key in the matching Substitutes map, the slot effectively loads the
/// <i>target</i> id — this is how a slot named <c>00001009</c> (Canadian French)
/// secretly loads US, or <c>d0010419</c> loads Ukrainian. The inspector tracks both
/// the <i>loaded</i> id (what the user experiences) and the source slot.
/// </para>
/// </summary>
public sealed class LayoutInspector
{
    private readonly IKeyboardLayoutRegistry _registry;

    public LayoutInspector(IKeyboardLayoutRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Build the flat list of resolved layouts, sorted Ghost-first.
    /// </summary>
    public IReadOnlyList<LayoutEntry> Inspect()
    {
        var hkcuPreload = _registry.GetPreloadValues(forDefaultHive: false);
        var hkcuSubs = _registry.GetSubstituteValues(forDefaultHive: false);
        var defaultPreload = _registry.GetPreloadValues(forDefaultHive: true);
        var defaultSubs = _registry.GetSubstituteValues(forDefaultHive: true);

        // Map: loadedLayoutId -> list of sources that load it (after substitute resolution).
        var byLoadedId = new Dictionary<string, List<LayoutSourceEntry>>(StringComparer.OrdinalIgnoreCase);

        AddPreloadSources(byLoadedId, hkcuPreload, hkcuSubs, LayoutSourceKind.HkcuPreload);
        AddPreloadSources(byLoadedId, defaultPreload, defaultSubs, LayoutSourceKind.DefaultPreload);

        // Orphan substitutes: a substitute entry whose source id is not present in ANY Preload.
        // These don't load anything by themselves but are dangling remnants worth surfacing.
        var orphans = CollectOrphanSubstitutes(hkcuSubs, hkcuPreload, defaultPreload, LayoutSourceKind.HkcuSubstitutes);
        orphans.AddRange(CollectOrphanSubstitutes(defaultSubs, hkcuPreload, defaultPreload, LayoutSourceKind.DefaultSubstitutes));

        var entries = new List<LayoutEntry>();

        foreach (var (loadedId, sources) in byLoadedId)
        {
            var isDeclared = sources.Any(s => s.Kind == LayoutSourceKind.HkcuPreload);
            entries.Add(new LayoutEntry(
                loadedId,
                BuildDisplayName(loadedId),
                isDeclared ? LayoutStatus.Declared : LayoutStatus.Ghost,
                sources.OrderBy(s => s.Kind).ThenBy(s => s.ValueName).ToList()));
        }

        foreach (var orphan in orphans)
        {
            // The orphan's loaded id is the substitute target; we surface it as Orphan.
            entries.Add(new LayoutEntry(
                orphan.TargetId,
                BuildDisplayName(orphan.TargetId) + " (orphan substitute)",
                LayoutStatus.Orphan,
                new[] { orphan.Source }));
        }

        return entries
            .OrderBy(e => (int)e.Status) // Ghost=0, Declared=1, Orphan=2
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddPreloadSources(
        Dictionary<string, List<LayoutSourceEntry>> byLoadedId,
        IReadOnlyDictionary<string, string> preload,
        IReadOnlyDictionary<string, string> substitutes,
        LayoutSourceKind kind)
    {
        foreach (var (slot, rawId) in preload)
        {
            // Resolve the effective loaded id through the substitutes map.
            var loadedId = substitutes.TryGetValue(rawId, out var target) ? target : rawId;
            var source = new LayoutSourceEntry(kind, slot);

            if (!byLoadedId.TryGetValue(loadedId, out var list))
            {
                list = new List<LayoutSourceEntry>();
                byLoadedId[loadedId] = list;
            }
            list.Add(source);
        }
    }

    private static List<(string TargetId, LayoutSourceEntry Source)> CollectOrphanSubstitutes(
        IReadOnlyDictionary<string, string> substitutes,
        IReadOnlyDictionary<string, string> hkcuPreload,
        IReadOnlyDictionary<string, string> defaultPreload,
        LayoutSourceKind kind)
    {
        var allPreloadIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in hkcuPreload.Values) allPreloadIds.Add(v);
        foreach (var v in defaultPreload.Values) allPreloadIds.Add(v);

        var result = new List<(string, LayoutSourceEntry)>();
        foreach (var (sourceId, targetId) in substitutes)
        {
            if (!allPreloadIds.Contains(sourceId))
            {
                result.Add((targetId, new LayoutSourceEntry(kind, sourceId)));
            }
        }
        return result;
    }

    private string BuildDisplayName(string layoutId)
    {
        // Canonicalise d-prefixed ids for HKLM lookup (e.g. d0010419 → 00000419).
        var canonical = layoutId.Length == 8 && layoutId[0] == 'd'
            ? "0000" + layoutId[4..]
            : layoutId;

        var layoutText = _registry.GetLayoutText(canonical);
        var language = _registry.GetLanguageName(canonical);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(language))
            parts.Add(language);
        if (!string.IsNullOrEmpty(layoutText))
            parts.Add(layoutText);

        var name = parts.Count > 0 ? string.Join(" — ", parts) : layoutId;
        return $"{name} ({layoutId})";
    }
}
