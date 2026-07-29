namespace Moongate.Tests.Support;

/// <summary>Where a real UO client directory lives, for the tests that need one.</summary>
public static class ClientFiles
{
    /// <summary>MOONGATE_UO_DIRECTORY when set, otherwise ~/uo.</summary>
    public static string Directory
        => Environment.GetEnvironmentVariable("MOONGATE_UO_DIRECTORY")
           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "uo");
}
