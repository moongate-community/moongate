using Moongate.Core.Extensions.Env;

namespace Moongate.Core.Extensions.Directories;

public static class DirectoriesExtension
{
    public static string ResolvePathAndEnvs(this string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        path = Environment.ExpandEnvironmentVariables(path).ExpandEnvironmentVariables();

        return path;
    }
}
