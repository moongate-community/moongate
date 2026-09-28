using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Resolves the real deletion source the first time the world save asks for it, so registering the persistence does
///     not build the live services and everything they depend on.
/// </summary>
public sealed class LazyDeletionSource : IPersistenceDeletionSource
{
    private readonly Lazy<IPersistenceDeletionSource> _source;

    public LazyDeletionSource(Func<IPersistenceDeletionSource> source)
    {
        _source = new(source);
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _source.Value.Capture();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        _source.Value.Committed(serials);
    }
}
