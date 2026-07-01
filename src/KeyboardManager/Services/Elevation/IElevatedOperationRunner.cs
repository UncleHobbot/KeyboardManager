namespace KeyboardManager.Services.Elevation;

/// <summary>
/// Runs <see cref="ElevatedOperation"/>s that must write to <c>.DEFAULT</c>,
/// returning the outcome. Implementations are responsible for the privilege
/// handoff (e.g. UAC relaunch); callers only see operations in and a result out.
/// </summary>
/// <remarks>
/// The interface exists so the elevated write path inside
/// <c>LayoutRemovalService</c> can be unit-tested without a real UAC prompt
/// (ADR-0002). The production adapter relaunches the current exe with
/// <c>runas</c>; a fake returns canned <see cref="ElevatedResult"/>s.
/// </remarks>
public interface IElevatedOperationRunner
{
    /// <summary>
    /// Apply the operations elevated. Returns the helper's result, or a result
    /// with a "declined" error if the user cancelled UAC.
    /// </summary>
    ElevatedResult Run(IReadOnlyList<ElevatedOperation> operations);
}
