using Moongate.Server.Core.Data.Admin;

namespace Moongate.Server.Core.Interfaces.Admin;

/// <summary>
///     Provides process metadata safe to expose to authenticated administrators.
/// </summary>
public interface IAdminServerInfoProvider
{
    /// <summary>
    ///     Reads a thread-safe snapshot without accessing live world entities.
    /// </summary>
    AdminServerInfo GetSnapshot();
}
