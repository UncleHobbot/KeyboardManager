using KeyboardManager.Services;
using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Tests;

/// <summary>
/// No-op <see cref="ISessionLayoutApplier"/> for tests. Records calls so tests
/// can assert that apply was attempted.
/// </summary>
internal sealed class FakeSessionApplier : ISessionLayoutApplier
{
    public List<string> UnloadedIds { get; } = new();
    public int BroadcastCount { get; private set; }

    public bool TryUnload(string layoutId)
    {
        UnloadedIds.Add(layoutId);
        return true;
    }

    public void BroadcastSettingsChange() => BroadcastCount++;
}

/// <summary>
/// <see cref="IElevatedOperationRunner"/> that returns a canned result without
/// spawning any process. Defaults to fully successful.
/// </summary>
internal sealed class FakeElevatedRunner : IElevatedOperationRunner
{
    public ElevatedResult Result { get; set; } = new(0, 0, Array.Empty<string>());

    public List<IReadOnlyList<ElevatedOperation>> SubmittedOps { get; } = new();

    public ElevatedResult Run(IReadOnlyList<ElevatedOperation> operations)
    {
        SubmittedOps.Add(operations);
        return Result;
    }
}
