using Moongate.UO.Data.Interfaces.FileLoaders;

namespace Moongate.Tests.Server.Support;

public sealed class FileLoaderServiceTestLoaderA : IFileLoader
{
    public Task LoadAsync()
    {
        Moongate.Tests.Server.Services.Files.FileLoaderServiceTests.ExecutionLog.Add("A");

        return Task.CompletedTask;
    }
}
