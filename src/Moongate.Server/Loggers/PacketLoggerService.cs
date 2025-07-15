using Moongate.Core.Extensions.Buffers;
using Moongate.Core.Network.Compression;
using Moongate.Core.Network.Extensions;
using Moongate.Core.Network.Servers.Tcp;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Interfaces.Services.Base;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Loggers;

public class PacketLoggerService : IMoongateService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly INetworkService _networkService;

    private readonly MoongateServerConfig _moongateServerConfig;

    public static byte[] IgnoredPacketTypes =
    [
        0x73
    ];

    public PacketLoggerService(
        IGameSessionService gameSessionService, INetworkService networkService, MoongateServerConfig moongateServerConfig
    )
    {
        _gameSessionService = gameSessionService;
        _networkService = networkService;
        _moongateServerConfig = moongateServerConfig;

        _networkService.OnClientDataReceived += OnPacketReceived;
        _networkService.OnClientDataSent += OnPacketSent;
    }

    private void OnPacketSent(string sessionId, ReadOnlyMemory<byte> packet)
    {
        var gameClientSession = _gameSessionService.GetSession(sessionId);
        LogPacketToFile(gameClientSession, packet, false);
        LogPacketToConsole(gameClientSession, packet, false);
    }

    private void OnPacketReceived(string sessionId, ReadOnlyMemory<byte> packet)
    {
        var gameClientSession = _gameSessionService.GetSession(sessionId);
        LogPacketToFile(gameClientSession, packet, false);
        LogPacketToConsole(gameClientSession, packet, true);
    }

    private void LogPacketToConsole(GameSession session, ReadOnlyMemory<byte> buffer, bool isReceived)
    {
        if (!_moongateServerConfig.Network.LogPacketsToConsole)
        {
            return;
        }

        if (IgnoredPacketTypes.Contains(buffer.Span[0]))
        {
            return; // Ignore specific packet types
        }

        var direction = isReceived ? "<-" : "->";
        var opCode = "OPCODE: " + buffer.Span[0].ToPacketString();

        var opCodeDescription = _networkService.GetPacketDescription(buffer.Span[0]);

        var logger = Log.ForContext("NetworkPacketConsole", true);

        logger.Debug(
            "{OpCode} {OpCodeDescription} | {Direction} | Id: {SessionId} | Size: {Size} bytes | Compression: {Compression}",
            opCode,
            opCodeDescription,
            direction,
            session.SessionId,
            buffer.Length,
            session.Features.HasFlag(NetworkSessionFeatureType.Compression)
        );
    }

    private void LogPacketToFile(GameSession client, ReadOnlyMemory<byte> buffer, bool isReceived)
    {
        if (!_moongateServerConfig.Network.LogPacketsToFile)
        {
            return;
        }

        if (IgnoredPacketTypes.Contains(buffer.Span[0]))
        {
            return; // Ignore specific packet types
        }

        var logger = Log.ForContext("NetworkPacket", true);

        using var sw = new StringWriter();
        var direction = isReceived ? "<-" : "->";
        var opCode = "OPCODE: " + buffer.Span[0].ToPacketString();

        int compressionSize = 0;
        if (client.Features.HasFlag(NetworkSessionFeatureType.Compression))
        {
            var tmpInBuffer = buffer.Span.ToArray();
            Span<byte> tmpOutBuffer = stackalloc byte[tmpInBuffer.Length];
            compressionSize = NetworkCompression.Compress(tmpInBuffer, tmpOutBuffer);
        }

        sw.WriteLine(
            $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | {opCode} | {direction} | Id: {client.SessionId} | Size: {buffer.Length} bytes | Compression: {client.Features.HasFlag(NetworkSessionFeatureType.Compression)} | Compression Size: {compressionSize} bytes"
        );
        sw.FormatBuffer(buffer.Span);
        sw.WriteLine(new string('-', 50));

        logger.Information(sw.ToString());
    }


    public void Dispose()
    {
    }
}
