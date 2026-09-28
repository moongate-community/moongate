using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Speech;
using Serilog;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>
///     Renders and queues the MOTD only for the character's original game connection.
/// </summary>
public sealed class MotdService : IMotdService
{
    private static readonly Hue MessageHue = new(0x03B2);

    private readonly IDataLoaderService _data;
    private readonly MotdRenderer _renderer;
    private readonly ISessionService _sessions;
    private readonly IAdminServerInfoProvider _serverInfo;
    private readonly RealmInstance _realm;
    private readonly MotdServerIdentity _identity;
    private readonly ILogger _logger = Log.ForContext<MotdService>();

    public MotdService(
        IDataLoaderService data,
        MotdRenderer renderer,
        ISessionService sessions,
        IAdminServerInfoProvider serverInfo,
        RealmInstance realm,
        MotdServerIdentity identity
    )
    {
        _data = data;
        _renderer = renderer;
        _sessions = sessions;
        _serverInfo = serverInfo;
        _realm = realm;
        _identity = identity;
    }

    public async ValueTask SendAsync(PacketContext context, MobileEntity character, CancellationToken cancellationToken)
    {
        var lines = _data.GetEntities<MotdLine>();

        if (lines.Count == 0)
        {
            return;
        }

        MotdContext? snapshot = null;

        try
        {
            var available = await context.RunOnGameLoopAsync(session =>
            {
                if (session.CharacterId != character.Id)
                {
                    return;
                }

                var usersOnline = _sessions.GetAll().Count(other =>
                    other.CharacterId.IsValid && other.NetworkSession.Client is { IsConnected: true });
                var info = _serverInfo.GetSnapshot();
                snapshot = new MotdContext(_identity.ServerName, _realm.Descriptor.Name, info.Version,
                    info.Codename, character.Name, usersOnline);
            }, cancellationToken);

            if (!available || snapshot is null)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    var text = await _renderer.RenderAsync(line, snapshot, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    if (!context.TrySend(SpeechMessageHelper.CreateSystem(text, MessageHue)))
                    {
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.Warning("Skipping MOTD line {Index} after {ErrorType}", line.Index, exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
