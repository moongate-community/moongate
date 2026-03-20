using Moongate.Tests.Server.Services.Files;
using Moongate.Server.Interfaces.Services.Files;

namespace Moongate.Tests.Server.Support;

public sealed class FileLoaderServiceTestLoaderB : IFileLoader
{
    public Task LoadAsync()
    {
        FileLoaderServiceTests.ExecutionLog.Add("B");

        return Task.CompletedTask;
    }
}
