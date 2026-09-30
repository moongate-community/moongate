using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Tests.TestSupport.Ultima.Decorations;

/// <summary>
///     Gives the decoration files it was built with.
/// </summary>
public sealed class StubDecorationsLoader : IDecorationsLoader
{
    private readonly IReadOnlyList<DecorationFile> _files;

    /// <summary>
    ///     When set, loading waits for it, to keep a decoration running.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public StubDecorationsLoader(params DecorationFile[] files)
    {
        _files = files;
    }

    public async Task<IReadOnlyList<DecorationFile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Gate is not null)
        {
            await Gate.Task;
        }

        return _files;
    }
}
