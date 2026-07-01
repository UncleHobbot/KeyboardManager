using System.IO;
using System.Text.Json;

namespace KeyboardManager.Services.Elevation;

/// <summary>
/// The elevated-side executor. Runs inside the relaunched (admin) process, applies
/// each <see cref="ElevatedOperation"/> against <c>.DEFAULT</c> via
/// <see cref="IKeyboardLayoutRegistry"/>, and writes a result file the caller polls.
///
/// <para>
/// This class does <b>not</b> decide to elevate — it only carries out a pre-
/// validated operation list. Validation is in <see cref="ElevatedOperationRunner"/>.
/// </para>
/// </summary>
public sealed class ElevatedHelper
{
    private readonly IKeyboardLayoutRegistry _registry;

    public ElevatedHelper(IKeyboardLayoutRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Apply the operations and return a result. Each operation is independent; one
    /// failure does not abort the rest.
    /// </summary>
    public ElevatedResult Run(IReadOnlyList<ElevatedOperation> operations)
    {
        var applied = 0;
        var errors = new List<string>();

        foreach (var op in operations)
        {
            try
            {
                var removed = op.KeyKind switch
                {
                    ElevatedKeyKind.DefaultPreload => _registry.DeletePreloadValue(forDefaultHive: true, op.ValueName),
                    ElevatedKeyKind.DefaultSubstitutes => _registry.DeleteSubstituteValue(forDefaultHive: true, op.ValueName),
                    _ => false
                };
                if (removed) applied++;
            }
            catch (Exception ex)
            {
                errors.Add($"{op.KeyKind}#{op.ValueName}: {ex.Message}");
            }
        }

        return new ElevatedResult(applied, operations.Count, errors);
    }

    /// <summary>
    /// Convenience: serialise a result to JSON at the given path for the caller to read.
    /// </summary>
    public static void WriteResult(ElevatedResult result, string path)
    {
        var json = JsonSerializer.Serialize(result, ElevatedOperationJsonContext.Default.ElevatedResult);
        File.WriteAllText(path, json);
    }
}

/// <param name="Applied">How many values were actually removed.</param>
/// <param name="Total">How many operations were submitted.</param>
/// <param name="Errors">Per-operation error messages, if any.</param>
public sealed record ElevatedResult(int Applied, int Total, IReadOnlyList<string> Errors);
