using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.Commands;

/// <summary>
/// A source-neutral command request. Carries who is asking (<see cref="ActorLevel" />, optional
/// <see cref="Actor" />), where from (<see cref="Source" />), the raw prefix-free
/// <see cref="CommandLine" />, and where output goes (<see cref="Reply" />). The in-game path and the
/// admin console both build one of these and hand it to
/// <see cref="Interfaces.Commands.ICommandService" />.
/// </summary>
public readonly record struct CommandInvocation(
    CommandSourceType Source,
    AccountLevelType ActorLevel,
    MobileEntity? Actor,
    string CommandLine,
    Action<string> Reply
);
