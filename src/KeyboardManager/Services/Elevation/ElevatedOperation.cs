namespace KeyboardManager.Services.Elevation;

/// <summary>
/// A single registry write to perform in the elevated helper, scoped to exactly one
/// value deletion. Deliberately narrow: the helper refuses anything that is not a
/// known key kind + a single value name, so it cannot be abused as a generic
/// registry-write surface (per ADR-0001's risk note).
/// </summary>
public sealed record ElevatedOperation(
    ElevatedKeyKind KeyKind,
    string ValueName);

/// <summary>
/// The two registry keys the elevated helper is permitted to write to. Both are
/// under <c>HKEY_USERS\.DEFAULT</c> — the only place that needs admin rights.
/// </summary>
public enum ElevatedKeyKind
{
    DefaultPreload,
    DefaultSubstitutes
}
