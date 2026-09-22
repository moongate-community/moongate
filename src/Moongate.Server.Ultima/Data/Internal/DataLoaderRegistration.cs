using DryIoc;

namespace Moongate.Server.Ultima.Data.Internal;

/// <summary>
/// One registered loader, with its generic <c>TEntity</c> closed over inside <see cref="RunAsync"/> so
/// <see cref="Services.DataLoaderService"/> can run every loader without knowing what any of them produce.
/// </summary>
internal sealed record DataLoaderRegistration(
    Type EntityType,
    Type LoaderType,
    int Priority,
    Func<IResolverContext, CancellationToken, Task<object>> RunAsync
);
