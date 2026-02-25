using Moongate.UO.Data.Interfaces.FileLoaders;

namespace Moongate.Tests.Server.Support;

public sealed class FileLoaderServiceTestLoaderB : IFileLoader
{
    public Task LoadAsync()
    {
        Moongate.Tests.Server.Services.Files.FileLoaderServiceTests.ExecutionLog.Add("B");

        return Task.CompletedTask;
    }
}
