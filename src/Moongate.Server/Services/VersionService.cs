using System.Reflection;
using Moongate.Core.Server.Data.Version;
using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Server.Services;

public class VersionService : IVersionService
{

    public VersionService()
    {
        var versionInfo = GetVersionInfo();
    }

    public VersionInfoData GetVersionInfo()
    {
        var version = typeof(VersionService).Assembly.GetName().Version;

        var codename = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "Codename")
            ?.Value;

        return new VersionInfoData(
            "Moongate",
            codename,
            version.ToString(),
            ThisAssembly.Git.Commit,
            ThisAssembly.Git.Branch,
            ThisAssembly.Git.CommitDate
        );
    }

    public void Dispose()
    {

    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
