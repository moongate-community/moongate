using Moongate.Server.Interfaces.Services.Files;

namespace Moongate.Tests.Server.Support;

public sealed class FileLoaderServiceTestLoaderThrows : IFileLoader
{
    public Task LoadAsync()
        => throw new InvalidOperationException("boom");
}
