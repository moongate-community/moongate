using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Services.Books;

/// <summary>
///     Captures the same built-in values as MOTD; live character names are read on the game loop.
/// </summary>
public sealed class BookContextFactory
{
    private readonly ISessionService _sessions;
    private readonly IAdminServerInfoProvider _serverInfo;
    private readonly RealmInstance _realm;
    private readonly MotdServerIdentity _identity;
    private readonly IGameLoopService _loop;

    public BookContextFactory(ISessionService sessions, IAdminServerInfoProvider serverInfo, RealmInstance realm, MotdServerIdentity identity, IGameLoopService loop)
    {
        _sessions = sessions;
        _serverInfo = serverInfo;
        _realm = realm;
        _identity = identity;
        _loop = loop;
    }

    public TextTemplateContext Capture(MobileEntity recipient, string? recordedPlayerName = null)
    {
        if (!_loop.IsOnLoopThread)
        {
            throw new InvalidOperationException("Document context must be captured on the game loop.");
        }

        return CaptureForCreation(recordedPlayerName ?? recipient.Name);
    }

    /// <summary>
    ///     Captures an immutable name for a character that is not live yet. Session reads and the admin snapshot are
    ///     thread-safe, so transactional character creation can call this off the game loop.
    /// </summary>
    public TextTemplateContext CaptureForCreation(string playerName)
    {
        var info = _serverInfo.GetSnapshot();
        return new()
        {
            ServerName = _identity.ServerName,
            RealmName = _realm.Descriptor.Name,
            Version = info.Version,
            Codename = info.Codename,
            PlayerName = playerName,
            UsersOnline = _sessions.GetAll().Count(session => session.CharacterId.IsValid && session.NetworkSession.Client is { IsConnected: true })
        };
    }
}
