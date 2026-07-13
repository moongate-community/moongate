using Moongate.UO.Data.Version;

namespace Moongate.UO.Data.Extensions;

public static class ClientVersionExtension
{
    /// <summary>
    ///  Helper for convert from System.Version to ClientVersion
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public static ClientVersion ToClientVersion(this System.Version version)
    {
        return new ClientVersion(version.Major, version.Minor, version.Build, version.Revision);
    }
}
