using Moongate.Ctl.Types.Completion;

namespace Moongate.Ctl.Data.Internal.Completion;

/// <summary>
///     An option of a command, such as <c>--target</c>, or its positional argument: its name, a short help, what its
///     value is and, for a choice, the words to offer.
/// </summary>
internal sealed record CompletionOption(string Name, string Help, CompletionValueType Value, params string[] Choices);
