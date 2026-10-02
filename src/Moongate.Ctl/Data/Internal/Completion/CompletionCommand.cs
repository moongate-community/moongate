namespace Moongate.Ctl.Data.Internal.Completion;

/// <summary>
///     A command as the shells complete it: its words, such as <c>migrate status</c>, a short help, its options and its
///     positional argument when it has one.
/// </summary>
internal sealed record CompletionCommand(
    string Path,
    string Help,
    IReadOnlyList<CompletionOption> Options,
    CompletionOption? Argument = null
);
