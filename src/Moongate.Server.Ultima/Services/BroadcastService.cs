using Moongate.Core.Primitives;
using Moongate.Network.Packets.Compression;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Speech;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Queues system messages for every connected character in the local world, regardless of map or distance.
/// </summary>
public sealed class BroadcastService : IBroadcastService
{
    private static readonly Hue MessageHue = new(0x03B2);

    private readonly IGameLoopService _loop;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public BroadcastService(
        IGameLoopService loop, ISessionService sessions, IMobileService mobiles, IPacketSendService sender
    )
    {
        _loop = loop;
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
    }

    /// <inheritdoc />
    public async Task<int> BroadcastAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();
        var packet = SpeechMessageHelper.CreateSystem(text, MessageHue);

        // The compression bound is capped at 64 KB. Large valid speech frames can still overflow that cap;
        // check their actual compressed size before an outbox can accept a payload that disconnects its client.
        if (HuffmanEncoder.CalculateMaxCompressedSize(packet.Length) == HuffmanEncoder.BufferSize &&
            HuffmanEncoder.Compress(PacketCodec.Encode(packet), new byte[HuffmanEncoder.BufferSize]) == 0)
        {
            throw new ArgumentException("The broadcast exceeds the compressed transport limit.", nameof(text));
        }

        var sent = 0;
        var work = new LoopActionWorkItem(() =>
            {
                foreach (var session in _sessions.GetAll())
                {
                    if (session.CharacterId.IsValid && _mobiles.IsInWorld(session.CharacterId) &&
                        SpeechMessageHelper.TrySend(_sender, session, packet))
                    {
                        sent++;
                    }
                }
            }
        );

        if (_loop.IsOnLoopThread)
        {
            work.Execute();
        }
        else
        {
            await _loop.PostAsync(work, cancellationToken);
        }

        await work.Completion;

        return sent;
    }
}
