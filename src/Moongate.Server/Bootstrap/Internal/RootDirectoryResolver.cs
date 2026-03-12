using Moongate.Core.Extensions.Directories;

namespace Moongate.Server.Bootstrap.Internal;

internal static class RootDirectoryResolver
{
    public static string Resolve(string? configuredRootDirectory)
    {
        var resolved = configuredRootDirectory;

        if (string.IsNullOrWhiteSpace(resolved))
        {
            resolved = Environment.GetEnvironmentVariable("MOONGATE_ROOT_DIRECTORY") ??
                       Path.Combine(AppContext.BaseDirectory, "moongate");
        }

        return resolved.ResolvePathAndEnvs();
    }
}
