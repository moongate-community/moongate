using DryIoc;
using Moongate.Server.Interfaces;

namespace Moongate.Server.Data.Internal;

/// <summary>
/// A declarative data-loader registration: its <see cref="Priority" /> orders execution (ascending)
/// and <see cref="Resolve" /> produces the loader instance from the container at startup.
/// </summary>
public sealed record DataLoaderRegistration(int Priority, Func<IResolverContext, IDataLoader> Resolve);
