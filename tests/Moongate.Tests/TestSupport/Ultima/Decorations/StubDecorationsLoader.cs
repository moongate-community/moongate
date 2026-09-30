using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Tests.TestSupport.Ultima.Decorations;

/// <summary>
///     Gives the decoration files it was built with.
/// </summary>
public sealed class StubDecorationsLoader : IDecorationsLoader
{
    private readonly IReadOnlyList<DecorationFile> _files;

    public StubDecorationsLoader(params DecorationFile[] files)
    {
        _files = files;
    }

    public Task<IReadOnlyList<DecorationFile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_files);
    }
}
