using DryIoc;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Login.Internal;
using Serilog;

namespace Moongate.Server.Services.Login;

/// <summary>Runs one bounded, ordered async handler mailbox per login connection.</summary>
public sealed class LoginPacketDispatchService : IMoongateStartupService
{
    private readonly Lock _gate = new();
    private readonly ILoginSessionService _sessions;
    private readonly LoginPacketHandlerRegistry _registry;
    private readonly IResolverContext _resolver;
    private readonly ILogger _logger = Log.ForContext<LoginPacketDispatchService>();
    private readonly Dictionary<LoginSession, LoginPacketMailbox> _mailboxes = new(ReferenceEqualityComparer.Instance);
    private IReadOnlyDictionary<Type, Func<LoginSession, IPacket, CancellationToken, ValueTask>>? _handlers;
    private bool _running;

    public LoginPacketDispatchService(ILoginSessionService sessions, LoginPacketHandlerRegistry registry,
        IResolverContext resolver)
    {
        _sessions = sessions;
        _registry = registry;
        _resolver = resolver;
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            _handlers = _registry.Freeze().ToDictionary(entry => entry.Key,
                entry => entry.Value.Bind(_resolver));
            _running = true;
        }

        return Task.CompletedTask;
    }

    public bool TryDispatch(long sessionId, IPacket packet)
    {
        lock (_gate)
        {
            if (!_running || _handlers is null || !_handlers.ContainsKey(packet.GetType()) ||
                !_sessions.TryGet(sessionId, out var session) || !_sessions.IsCurrent(session))
            {
                return false;
            }

            if (!_mailboxes.TryGetValue(session, out var mailbox))
            {
                mailbox = new(session, _handlers, _logger, 128);
                _mailboxes.Add(session, mailbox);
                mailbox.Start();
            }

            return mailbox.TryWrite(packet);
        }
    }

    public Task DisconnectAsync(long sessionId)
        => _sessions.TryGet(sessionId, out var session)
               ? DisconnectAsync(session)
               : Task.CompletedTask;

    public Task DisconnectAsync(LoginSession session)
    {
        LoginPacketMailbox? mailbox;
        lock (_gate)
        {
            if (!_mailboxes.Remove(session, out mailbox))
            {
                return Task.CompletedTask;
            }
        }

        return mailbox.StopAsync();
    }

    public async Task StopAsync()
    {
        LoginPacketMailbox[] mailboxes;
        lock (_gate)
        {
            _running = false;
            mailboxes = _mailboxes.Values.ToArray();
            _mailboxes.Clear();
        }

        await Task.WhenAll(mailboxes.Select(mailbox => mailbox.StopAsync())).ConfigureAwait(false);
    }
}
