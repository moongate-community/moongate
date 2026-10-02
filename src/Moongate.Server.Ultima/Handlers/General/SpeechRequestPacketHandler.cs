using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Speech;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Speech;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.General;

/// <summary>
///     Routes normal speech to nearby players and dot-prefixed text to the in-game command system.
/// </summary>
public sealed class SpeechRequestPacketHandler :
    IAsyncPacketHandler<AsciiSpeechRequestPacket>,
    IAsyncPacketHandler<UnicodeSpeechRequestPacket>
{
    private const int SayRange = 15;
    private const int MaximumTextLength = 128;

    private static readonly Hue InformationHue = new(0x03B2);
    private static readonly Hue WarningHue = new(0x0035);
    private static readonly Hue ErrorHue = new(0x0021);

    private readonly ILogger _logger = Log.ForContext<SpeechRequestPacketHandler>();
    private readonly ConcurrentDictionary<long, Task> _running = new();
    private readonly ICommandSystemService _commands;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly ILocalizationService? _localization;
    private readonly INpcSpeechListener? _npcs;
    private readonly IMoongateEventBus? _events;
    private readonly IItemSpeechListener? _items;

    public SpeechRequestPacketHandler(
        ICommandSystemService commands,
        ISessionService sessions,
        IMobileService mobiles,
        IPacketSendService sender,
        ILocalizationService? localization = null,
        INpcSpeechListener? npcs = null,
        IMoongateEventBus? events = null,
        IItemSpeechListener? items = null
    )
    {
        _items = items;
        _events = events;
        _localization = localization;
        _npcs = npcs;
        _commands = commands;
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
    }

    public ValueTask HandleAsync(
        PacketContext context,
        AsciiSpeechRequestPacket packet,
        CancellationToken cancellationToken
    )
    {
        return HandleSpeechAsync(context, packet.Speech, cancellationToken);
    }

    public ValueTask HandleAsync(
        PacketContext context,
        UnicodeSpeechRequestPacket packet,
        CancellationToken cancellationToken
    )
    {
        return HandleSpeechAsync(context, packet.Speech, cancellationToken);
    }

    private async ValueTask HandleSpeechAsync(
        PacketContext context,
        SpeechRequestData speech,
        CancellationToken cancellationToken
    )
    {
        if (speech.Type != SpeechType.Regular ||
            string.IsNullOrWhiteSpace(speech.Text) ||
            speech.Text.Length > MaximumTextLength)
        {
            return;
        }

        var escaped = speech.Text.StartsWith("..", StringComparison.Ordinal);
        var command = !escaped && speech.Text[0] == '.';
        var text = escaped ? speech.Text[1..] : speech.Text;

        if (command && text.Length == 1)
        {
            return;
        }

        GameSession? invoker = null;
        MobileEntity? said = null;
        var available = await context.RunOnGameLoopAsync(
            session =>
            {
                if (!session.CharacterId.IsValid || !_mobiles.TryGet(session.CharacterId, out var speaker))
                {
                    return;
                }

                invoker = session;

                if (command)
                {
                    return;
                }

                if (speaker.Body is < 0 or > ushort.MaxValue)
                {
                    return;
                }

                var message = SpeechMessageHelper.CreatePlayer(
                    speaker.Id,
                    (ushort)speaker.Body,
                    speaker.Name,
                    speech with { Text = text }
                );

                foreach (var recipient in _sessions.GetAll())
                {
                    if (recipient.CharacterId.IsValid &&
                        _mobiles.TryGet(recipient.CharacterId, out var mobile) &&
                        mobile.Map == speaker.Map &&
                        mobile.Location.InRange(speaker.Location, SayRange))
                    {
                        SpeechMessageHelper.TrySend(_sender, recipient, message);
                    }
                }

                _npcs?.Heard(speaker, text, speech.Keywords);
                _items?.Heard(speaker, text, speech.Keywords);
                said = speaker;
            },
            cancellationToken
        );

        // Off the loop, after everyone around heard it: scripts are told last (the player_say event).
        if (said is not null && _events is not null)
        {
            await _events.PublishAsync(new PlayerSaidEvent(said, text), CancellationToken.None);
        }

        if (!available || !command || invoker is null)
        {
            return;
        }

        // One command at a time per session, as when commands held the session's packets.
        if (_running.TryGetValue(invoker.SessionId, out var running) && !running.IsCompleted)
        {
            context.TrySend(
                SpeechMessageHelper.CreateSystem(
                    _localization.Text(CommandMessages.CommandAlreadyRunning, "A command is already running."),
                    WarningHue
                )
            );

            return;
        }

        Track(invoker.SessionId, RunCommandAsync(context, text[1..], invoker));
    }

    /// <summary>
    ///     Waits for the in-game commands still running.
    /// </summary>
    internal Task WaitForCommandsAsync()
    {
        return Task.WhenAll(_running.Values);
    }

    // Detached from the packet: a command waiting for the player, such as for a target, must not hold back the
    // session's next packets, the answer included. Its output is sent when it ends.
    private async Task RunCommandAsync(PacketContext context, string commandLine, GameSession invoker)
    {
        try
        {
            var output = await _commands.ExecuteAsync(
                commandLine,
                CommandSourceType.InGame,
                invoker,
                CancellationToken.None
            );

            foreach (var line in output)
            {
                var hue = line.Level switch
                {
                    CommandOutputLevel.Warning => WarningHue,
                    CommandOutputLevel.Error => ErrorHue,
                    _ => InformationHue
                };

                if (!context.TrySend(SpeechMessageHelper.CreateSystem(line.Text, hue)))
                {
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "In-game command {Command} failed", commandLine);
        }
    }

    private void Track(long sessionId, Task task)
    {
        _running[sessionId] = task;
        task.ContinueWith(done => _running.TryRemove(new(sessionId, done)), TaskScheduler.Default);
    }
}
