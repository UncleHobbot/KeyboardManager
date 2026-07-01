using KeyboardManager.Models;

namespace KeyboardManager.Services;

/// <summary>
/// The single owner of the substitute-resolution rule, the <c>d</c>-prefix
/// canonicalisation, status classification, and display-name assembly. Reads the
/// four raw registry maps once and returns a rich immutable snapshot from which
/// both the UI and the operation/removal layer read. See ADR-0003.
/// </summary>
public sealed class LayoutResolver
{
    private readonly IKeyboardLayoutRegistry _registry;

    public LayoutResolver(IKeyboardLayoutRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Build the resolved snapshot, sorted Ghost-first.
    /// </summary>
    public ResolvedLayoutSet Resolve()
    {
        var hkcuPreload = _registry.GetPreloadValues(forDefaultHive: false);
        var hkcuSubs = _registry.GetSubstituteValues(forDefaultHive: false);
        var defaultPreload = _registry.GetPreloadValues(forDefaultHive: true);
        var defaultSubs = _registry.GetSubstituteValues(forDefaultHive: true);

        // Map: loadedLayoutId -> list of sources that load it (after substitute resolution).
        var byLoadedId = new Dictionary<string, List<ResolvedSource>>(StringComparer.OrdinalIgnoreCase);

        AddPreloadSources(byLoadedId, hkcuPreload, hkcuSubs, LayoutSourceKind.HkcuPreload);
        AddPreloadSources(byLoadedId, defaultPreload, defaultSubs, LayoutSourceKind.DefaultPreload);

        // Orphan substitutes: a substitute entry whose source id is in no Preload at all.
        var orphans = CollectOrphanSubstitutes(hkcuSubs, hkcuPreload, defaultPreload, LayoutSourceKind.HkcuSubstitutes);
        orphans.AddRange(CollectOrphanSubstitutes(defaultSubs, hkcuPreload, defaultPreload, LayoutSourceKind.DefaultSubstitutes));

        var layouts = new List<ResolvedLayout>();

        foreach (var (loadedId, sources) in byLoadedId)
        {
            var canonical = Canonicalise(loadedId);
            var isDeclared = sources.Any(s => s.Kind == LayoutSourceKind.HkcuPreload);
            layouts.Add(new ResolvedLayout(
                LoadedLayoutId: loadedId,
                CanonicalLayoutId: canonical,
                DisplayName: BuildDisplayName(loadedId, canonical),
                Status: isDeclared ? LayoutStatus.Declared : LayoutStatus.Ghost,
                Sources: sources.OrderBy(s => s.Kind).ThenBy(s => s.SlotName, StringComparer.OrdinalIgnoreCase).ToList()));
        }

        foreach (var orphan in orphans)
        {
            var canonical = Canonicalise(orphan.TargetId);
            layouts.Add(new ResolvedLayout(
                LoadedLayoutId: orphan.TargetId,
                CanonicalLayoutId: canonical,
                DisplayName: BuildDisplayName(orphan.TargetId, canonical) + " (orphan substitute)",
                Status: LayoutStatus.Orphan,
                Sources: new[] { orphan.Source }));
        }

        var sorted = layouts
            .OrderBy(l => (int)l.Status) // Ghost=0, Declared=1, Orphan=2
            .ThenBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResolvedLayoutSet(sorted);
    }

    /// <summary>
    /// For each Preload slot, resolve the effective loaded id through the
    /// substitutes map and record a rich source entry.
    /// </summary>
    private static void AddPreloadSources(
        Dictionary<string, List<ResolvedSource>> byLoadedId,
        IReadOnlyDictionary<string, string> preload,
        IReadOnlyDictionary<string, string> substitutes,
        LayoutSourceKind kind)
    {
        foreach (var (slot, rawId) in preload)
        {
            var viaSub = substitutes.TryGetValue(rawId, out var target) ? target : null;
            var loadedId = viaSub ?? rawId;

            var source = new ResolvedSource(
                Kind: kind,
                SlotName: slot,
                RawLayoutId: rawId,
                LoadedLayoutId: loadedId,
                ViaSubstitute: viaSub);

            if (!byLoadedId.TryGetValue(loadedId, out var list))
            {
                list = new List<ResolvedSource>();
                byLoadedId[loadedId] = list;
            }
            list.Add(source);
        }
    }

    /// <summary>
    /// Existing orphans: substitutes whose source id appears in no Preload key.
    /// Surfaced to the user as Orphan-status entries.
    /// </summary>
    private static List<(string TargetId, ResolvedSource Source)> CollectOrphanSubstitutes(
        IReadOnlyDictionary<string, string> substitutes,
        IReadOnlyDictionary<string, string> hkcuPreload,
        IReadOnlyDictionary<string, string> defaultPreload,
        LayoutSourceKind kind)
    {
        var allPreloadIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in hkcuPreload.Values) allPreloadIds.Add(v);
        foreach (var v in defaultPreload.Values) allPreloadIds.Add(v);

        var result = new List<(string, ResolvedSource)>();
        foreach (var (sourceId, targetId) in substitutes)
        {
            if (!allPreloadIds.Contains(sourceId))
            {
                result.Add((targetId, new ResolvedSource(
                    Kind: kind,
                    SlotName: sourceId,
                    RawLayoutId: sourceId,
                    LoadedLayoutId: targetId,
                    ViaSubstitute: targetId)));
            }
        }
        return result;
    }

    /// <summary>
    /// Strip the <c>d</c> prefix from an extended layout id (e.g.
    /// <c>d0010419</c> → <c>00000419</c>). The single copy of this rule in the
    /// codebase — see ADR-0003.
    /// </summary>
    private static string Canonicalise(string layoutId)
        => layoutId.Length == 8 && layoutId[0] == 'd' ? "0000" + layoutId[4..] : layoutId;

    private string BuildDisplayName(string layoutId, string canonical)
    {
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
