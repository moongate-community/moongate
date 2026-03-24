using Moongate.Server.Interfaces.Services.Files;
using Moongate.Tests.Server.Services.Files;

namespace Moongate.Tests.Server.Support;

public sealed class FileLoaderServiceTestLoaderA : IFileLoader
{
    public Task LoadAsync()
    {
        FileLoaderServiceTests.ExecutionLog.Add("A");

        return Task.CompletedTask;
    }
}
