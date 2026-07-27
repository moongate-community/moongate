using Moongate.Core.Types;

namespace Moongate.Server.Abstractions.Data.Commands;

/// <summary>A command's public metadata for listings (e.g. the console's <c>help</c>).</summary>
public readonly record struct CommandDescriptor(
    string Name,
    AccountLevelType MinLevel,
    string Description
);
