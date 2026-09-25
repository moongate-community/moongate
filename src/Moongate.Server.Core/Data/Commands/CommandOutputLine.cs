using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Data.Commands;

/// <summary>
///     A single line of command output carried with its severity.
/// </summary>
public sealed record CommandOutputLine(string Text, CommandOutputLevel Level);
