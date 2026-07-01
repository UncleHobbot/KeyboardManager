using KeyboardManager.Services.Elevation;

namespace KeyboardManager.Tests;

/// <summary>
/// Tests the elevated helper's <em>logic</em> (not the actual UAC relaunch).
/// Exercises <see cref="ElevatedHelper.Run"/> against the fake registry, which is
/// the part that matters: the runner is a thin process-launch wrapper.
/// </summary>
public class ElevatedHelperTests
{
    [Fact]
    public void Run_DeletesDefaultPreloadAndSubstituteValues()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["1"] = "00001009", ["2"] = "d0010419" },
            DefaultSubstitutes = { ["00001009"] = "00000409", ["d0010419"] = "00000422" }
        };
        var helper = new ElevatedHelper(reg);

        var result = helper.Run(new[]
        {
            new ElevatedOperation(ElevatedKeyKind.DefaultPreload, "1"),
            new ElevatedOperation(ElevatedKeyKind.DefaultPreload, "2"),
            new ElevatedOperation(ElevatedKeyKind.DefaultSubstitutes, "00001009"),
            new ElevatedOperation(ElevatedKeyKind.DefaultSubstitutes, "d0010419")
        });

        Assert.Equal(4, result.Applied);
        Assert.Empty(result.Errors);
        Assert.Empty(reg.DefaultPreload);
        Assert.Empty(reg.DefaultSubstitutes);
    }

    [Fact]
    public void Run_MissingValueIsNotAppliedButNotAnError()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["1"] = "00001009" }
        };
        var helper = new ElevatedHelper(reg);

        var result = helper.Run(new[]
        {
            new ElevatedOperation(ElevatedKeyKind.DefaultPreload, "1"),
            new ElevatedOperation(ElevatedKeyKind.DefaultPreload, "99") // absent
        });

        Assert.Equal(1, result.Applied);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Run_ContinuesPastFailure()
    {
        var reg = new FakeKeyboardLayoutRegistry
        {
            DefaultPreload = { ["1"] = "00001009" }
        };
        // Use a throw-on-delete fake to simulate a failure mid-run.
        var throwing = new ThrowingRegistry(reg);

        var helper = new ElevatedHelper(throwing);
        var result = helper.Run(new[]
        {
            new ElevatedOperation(ElevatedKeyKind.DefaultPreload, "1"),
            new ElevatedOperation(ElevatedKeyKind.DefaultSubstitutes, "x")
        });

        Assert.NotEmpty(result.Errors);
    }

    /// <summary>
    /// Wraps the fake and throws on substitute deletion to exercise error capture.
    /// </summary>
    private sealed class ThrowingRegistry : IKeyboardLayoutRegistryDelegator
    {
        private readonly FakeKeyboardLayoutRegistry _inner;
        public ThrowingRegistry(FakeKeyboardLayoutRegistry inner) => _inner = inner;

        public IReadOnlyDictionary<string, string> GetPreloadValues(bool f) => _inner.GetPreloadValues(f);
        public IReadOnlyDictionary<string, string> GetSubstituteValues(bool f) => _inner.GetSubstituteValues(f);
        public string? GetLayoutText(string id) => _inner.GetLayoutText(id);
        public string? GetLanguageName(string id) => _inner.GetLanguageName(id);
        public bool DeletePreloadValue(bool f, string n) => _inner.DeletePreloadValue(f, n);
        public bool DeleteSubstituteValue(bool f, string n) => throw new UnauthorizedAccessException("simulated");
        public void ReplacePreloadValues(bool f, IReadOnlyDictionary<string, string> v) => _inner.ReplacePreloadValues(f, v);
        public void ClearSubstitutes(bool f) => _inner.ClearSubstitutes(f);
    }

    private interface IKeyboardLayoutRegistryDelegator : KeyboardManager.Services.IKeyboardLayoutRegistry { }
}
