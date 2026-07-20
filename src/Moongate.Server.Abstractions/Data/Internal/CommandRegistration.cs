using DryIoc;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>
/// A declarative GM/admin command registration: the dispatch metadata (<see cref="Name" /> aliases,
/// minimum <see cref="MinLevel" />, help <see cref="Description" />, allowed <see cref="Sources" />)
/// plus a <see cref="Resolve" /> that produces the command instance from the container on first
/// dispatch. Mirrors <see cref="DataLoaderRegistration" /> and replaces the old runtime
/// <c>[Command]</c> attribute scan — the metadata is stated explicitly at registration time.
/// </summary>
public sealed record CommandRegistration(
    string Name,
    AccountLevelType MinLevel,
    string Description,
    CommandSourceType Sources,
    Func<IResolverContext, ICommand> Resolve
);
