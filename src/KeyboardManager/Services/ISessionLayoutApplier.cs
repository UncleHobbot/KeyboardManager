namespace KeyboardManager.Services;

/// <summary>
/// Best-effort runtime application of layout changes. Implementations attempt to
/// evict a removed layout from the running session and broadcast the change so
/// the shell and other apps re-read their input configuration.
/// </summary>
/// <remarks>
/// The interface exists so the operation flows can be unit-tested without P/Invoke
/// (ADR-0002). The contract is deliberately best-effort: callers should treat the
/// session as potentially stale until a sign-out.
/// </remarks>
public interface ISessionLayoutApplier
{
    /// <summary>
    /// Attempt to evict a layout by id from the running session. Returns true if
    /// the underlying <c>UnloadKeyboardLayout</c> call succeeded — necessary but
    /// not sufficient for the layout to actually disappear from the switcher.
    /// </summary>
    bool TryUnload(string layoutId);

    /// <summary>
    /// Broadcast a settings-change notification so the shell and other apps
    /// re-read their input configuration.
    /// </summary>
    void BroadcastSettingsChange();
}
