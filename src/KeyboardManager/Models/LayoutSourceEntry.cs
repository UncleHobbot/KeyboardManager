namespace KeyboardManager.Models;

/// <summary>
/// One location in the registry where a layout (or a substitute mapping toward it)
/// was found. The <see cref="ValueName"/> is the registry value name under the key;
/// for Preload keys it is the numeric slot ("1", "2", ...), for Substitutes it is
/// the source layout id being remapped.
/// </summary>
public sealed record LayoutSourceEntry(
    LayoutSourceKind Kind,
    string ValueName);
