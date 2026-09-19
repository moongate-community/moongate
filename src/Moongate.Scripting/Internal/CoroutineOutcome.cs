using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;

namespace Moongate.Scripting.Internal;

internal sealed record CoroutineOutcome(CoroutineOutcomeKind Kind, IReadOnlyList<object?> Values, ScriptErrorInfo? Error)
{
    public static CoroutineOutcome Suspended { get; } = new(CoroutineOutcomeKind.Suspended, [], null);
}
