using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.Commands;

/// <summary>
/// One command invocation. <see cref="Reply" /> is a plain delegate rather than a packet-sending
/// call so a non-in-game <see cref="CommandSourceType" /> (e.g. the admin console) can supply a
/// different sink without <see cref="Interfaces.Commands.ICommand" /> implementations changing.
/// <see cref="Actor" /> is null for actor-less sources such as the admin console.
/// </summary>
public readonly record struct CommandContext(
    CommandSourceType Source,
    MobileEntity? Actor,
    IReadOnlyList<string> Arguments,
    Action<string> Reply
);
