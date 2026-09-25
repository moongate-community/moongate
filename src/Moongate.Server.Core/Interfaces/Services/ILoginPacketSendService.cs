namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>
///     Sends packets only through connections admitted by the login-role transport.
/// </summary>
public interface ILoginPacketSendService : IPacketSendService
{
}
