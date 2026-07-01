namespace KeyboardManager.Services;

/// <summary>
/// Read/write abstraction over the Windows keyboard-layout registry keys. Keeping
/// this behind an interface lets the inspection logic be unit-tested with a fake
/// hive, per issue 02's acceptance criteria.
/// </summary>
public interface IKeyboardLayoutRegistry
{
    /// <summary>
    /// Reads the name→value pairs under a Preload key. Value names are numeric
    /// slots ("1", "2", ...); values are layout ids.
    /// </summary>
    IReadOnlyDictionary<string, string> GetPreloadValues(bool forDefaultHive);

    /// <summary>
    /// Reads the substitute mappings. The key is the source layout id (as written
    /// to a Preload slot), the value is the target layout id it is remapped to.
    /// </summary>
    IReadOnlyDictionary<string, string> GetSubstituteValues(bool forDefaultHive);

    /// <summary>
    /// Resolves a layout's <c>Layout Text</c> from
    /// <c>HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\&lt;id&gt;</c>.
    /// Returns null when the id is unknown to HKLM.
    /// </summary>
    string? GetLayoutText(string canonicalLayoutId);

    /// <summary>
    /// Resolves an LCID-derived language name for a layout id. The low 4 hex digits
    /// encode the language id (e.g. 0419 → "Russian"). Returns null when unknown.
    /// </summary>
    string? GetLanguageName(string canonicalLayoutId);
}
