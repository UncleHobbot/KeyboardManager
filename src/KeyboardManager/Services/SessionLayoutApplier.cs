using System.Runtime.InteropServices;

namespace KeyboardManager.Services;

/// <summary>
/// Best-effort runtime application of layout changes (FR-2 step 5). The registry
/// is the durable source of truth, but the running session holds its own loaded
/// layout handles (HKLs) and does not always notice registry edits immediately.
///
/// <para>
/// This tries to evict a removed layout by calling <c>UnloadKeyboardLayout</c> on
/// its handle and broadcasting <c>WM_SETTINGCHANGE</c>. If that fails (the layout
/// is in use, or the call is not permitted), the caller should advise a sign-out.
/// </para>
/// </summary>
public sealed class SessionLayoutApplier : ISessionLayoutApplier
{
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int HWND_BROADCAST = 0xFFFF;
    private const int SMTO_ABORTIFHUNG = 0x0002;
    private const uint SPI_SETDEFAULTINPUTLANG = 0x005A;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnloadKeyboardLayout(IntPtr hkl);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint flags);

    /// <summary>
    /// Attempt to evict a layout by id from the running session. Returns true if the
    /// call to <c>UnloadKeyboardLayout</c> succeeded (which is necessary but not
    /// sufficient for the layout to actually disappear).
    /// </summary>
    public bool TryUnload(string layoutId)
    {
        var canonical = layoutId.Length == 8 && layoutId[0] == 'd'
            ? "0000" + layoutId[4..]
            : layoutId;

        // Loading then unloading by id is the common trick to obtain the HKL for a
        // layout id. UnloadKeyboardLayout requires the HKL of the LAST loaded
        // instance, so this pairs with that expectation on a best-effort basis.
        var hkl = LoadKeyboardLayout(canonical, 0x00000001 /* KLF_ACTIVATE */);
        if (hkl == IntPtr.Zero) return false;

        var ok = UnloadKeyboardLayout(hkl);

        BroadcastSettingsChange();

        return ok;
    }

    /// <summary>
    /// Broadcast a settings-change notification so the shell and other apps
    /// re-read their input configuration.
    /// </summary>
    public void BroadcastSettingsChange()
    {
        SendMessageTimeout(
            (IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE,
            (IntPtr)SPI_SETDEFAULTINPUTLANG, IntPtr.Zero,
            SMTO_ABORTIFHUNG, 1000, out _);
    }
}
