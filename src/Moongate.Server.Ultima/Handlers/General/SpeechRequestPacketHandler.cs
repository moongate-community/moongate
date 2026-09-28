using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Data.Speech;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Speech;

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

    private readonly ICommandSystemService _commands;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public SpeechRequestPacketHandler(
        ICommandSystemService commands,
        ISessionService sessions,
        IMobileService mobiles,
        IPacketSendService sender
    )
    {
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
            },
            cancellationToken
        );

        if (!available || !command || invoker is null)
        {
            return;
        }

        var output = await _commands.ExecuteAsync(text[1..], CommandSourceType.InGame, invoker, cancellationToken);

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
}
