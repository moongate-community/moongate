using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Moongate.Stress.Data;
using Moongate.Stress.Interfaces;
using Moongate.Stress.Internal;
using Moongate.UO.Data.Types;

namespace Moongate.Stress.Services;

public sealed class UoStressClient : IStressClient
{
    private static readonly DirectionType[] WalkDirections =
    [
        DirectionType.North,
        DirectionType.NorthEast,
        DirectionType.East,
        DirectionType.SouthEast,
        DirectionType.South,
        DirectionType.SouthWest,
        DirectionType.West,
        DirectionType.NorthWest
    ];

    private readonly StressRunOptions _options;
    private readonly StressMetricsCollector _metrics;
    private readonly string _username;
    private readonly string _password;
    private readonly string _characterName;
    private readonly Random _random;

    public int ClientIndex { get; }

    public UoStressClient(int clientIndex, StressRunOptions options, StressMetricsCollector metrics)
    {
        ClientIndex = clientIndex;
        _options = options;
        _metrics = metrics;
        _username = HttpAccountBootstrapper.BuildUsername(options.UserPrefix, clientIndex);
        _password = options.UserPassword;
        _characterName = BuildCharacterName(_username);
        _random = new(unchecked(Environment.TickCount * 397) ^ clientIndex);
    }

    private readonly record struct LoginRedirect(string Host, int Port, uint SessionKey);

    public async ValueTask DisposeAsync()
        => await Task.CompletedTask;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var redirect = await RunLoginStageAsync(cancellationToken);
            using var gameClient = new TcpClient();
            await gameClient.ConnectAsync(redirect.Host, redirect.Port, cancellationToken);
            gameClient.NoDelay = true;

            using var gameStream = gameClient.GetStream();
            var inGame = await EnterGameAsync(gameStream, redirect.SessionKey, cancellationToken);

            if (!inGame)
            {
                _metrics.MarkLoginFailed();

                return;
            }

            _metrics.MarkLoginSucceeded();

            await MoveLoopAsync(gameStream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _metrics.MarkLoginFailed();
            _metrics.MarkUnexpectedDisconnect();
        }
    }

    private static void Append(List<byte> pending, byte[] source, int count)
    {
        for (var i = 0; i < count; i++)
        {
            pending.Add(source[i]);
        }
    }

    private static string BuildCharacterName(string username)
    {
        var candidate = username.Replace("_", string.Empty, StringComparison.Ordinal);

        if (candidate.Length > 30)
        {
            candidate = candidate[..30];
        }

        return candidate;
    }

    private async Task<bool> EnterGameAsync(NetworkStream stream, uint sessionKey, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(UoPacketWriter.SeedOnly(sessionKey), cancellationToken);
        await stream.WriteAsync(UoPacketWriter.GameLogin(sessionKey, _username, _password), cancellationToken);

        var compressedPending = new List<byte>(8192);
        var decoder = new UoCompressionStreamDecoder();
        var buffer = new byte[8192];

        var gotLoginConfirm = false;
        var loginDeadline = DateTime.UtcNow.AddSeconds(10);
        var sentLoginCharacter = false;
        var sentCharacterCreation = false;

        while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow <= loginDeadline)
        {
            if (!sentLoginCharacter)
            {
                await stream.WriteAsync(UoPacketWriter.LoginCharacter(_characterName), cancellationToken);
                sentLoginCharacter = true;
            }

            if (!gotLoginConfirm &&
                sentLoginCharacter &&
                !sentCharacterCreation &&
                DateTime.UtcNow > loginDeadline.AddSeconds(-7))
            {
                await stream.WriteAsync(UoPacketWriter.CharacterCreation(_characterName), cancellationToken);
                sentCharacterCreation = true;
            }

            if (!stream.DataAvailable)
            {
                await Task.Delay(20, cancellationToken);

                continue;
            }

            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read <= 0)
            {
                return false;
            }

            Append(compressedPending, buffer, read);

            while (decoder.TryDecodeOne(compressedPending, out var consumed, out var decompressed))
            {
                compressedPending.RemoveRange(0, consumed);

                if (decompressed.Length == 0)
                {
                    continue;
                }

                var opcode = decompressed[0];

                if (opcode == 0x1B)
                {
                    gotLoginConfirm = true;

                    break;
                }
            }

            if (gotLoginConfirm)
            {
                return true;
            }
        }

        return false;
    }

    private async Task MoveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var compressedPending = new List<byte>(4096);
        var decoder = new UoCompressionStreamDecoder();
        var receiveBuffer = new byte[4096];
        byte sequence = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var direction = WalkDirections[_random.Next(0, WalkDirections.Length)] | DirectionType.Running;
            var movePacket = UoPacketWriter.Move(direction, sequence);

            _metrics.MarkMoveSent(ClientIndex, sequence);
            await stream.WriteAsync(movePacket, cancellationToken);

            var ackDeadline = DateTime.UtcNow.AddMilliseconds(1500);
            var acked = false;

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow <= ackDeadline)
            {
                if (!stream.DataAvailable)
                {
                    await Task.Delay(5, cancellationToken);

                    continue;
                }

                var read = await stream.ReadAsync(receiveBuffer, cancellationToken);

                if (read <= 0)
                {
                    _metrics.MarkUnexpectedDisconnect();

                    return;
                }

                Append(compressedPending, receiveBuffer, read);

                while (decoder.TryDecodeOne(compressedPending, out var consumed, out var payload))
                {
                    compressedPending.RemoveRange(0, consumed);

                    if (payload.Length == 0)
                    {
                        continue;
                    }

                    var opcode = payload[0];

                    if (opcode == 0x22 && payload.Length >= 3)
                    {
                        var ackSequence = payload[1];
                        _metrics.MarkMoveAcked(ClientIndex, ackSequence);

                        if (ackSequence == sequence)
                        {
                            acked = true;
                        }
                    }
                }

                if (acked)
                {
                    break;
                }
            }

            sequence++;
            await Task.Delay(_options.MoveIntervalMs, cancellationToken);
        }
    }

    private static LoginRedirect ParseRedirect(IReadOnlyList<byte> packet)
    {
        var ipRaw = BinaryPrimitives.ReadUInt32LittleEndian(new[] { packet[1], packet[2], packet[3], packet[4] });
        var ipBytes = BitConverter.GetBytes(ipRaw);
        var ip = new IPAddress(ipBytes);
        var port = BinaryPrimitives.ReadUInt16BigEndian(new[] { packet[5], packet[6] });
        var sessionKey = BinaryPrimitives.ReadUInt32BigEndian(new[] { packet[7], packet[8], packet[9], packet[10] });

        return new(ip.ToString(), port, sessionKey);
    }

    private async Task<LoginRedirect> RunLoginStageAsync(CancellationToken cancellationToken)
    {
        using var loginClient = new TcpClient();
        await loginClient.ConnectAsync(_options.Host, _options.Port, cancellationToken);
        loginClient.NoDelay = true;

        using var stream = loginClient.GetStream();

        await stream.WriteAsync(UoPacketWriter.LoginSeed((uint)_random.Next(1, int.MaxValue)), cancellationToken);
        await stream.WriteAsync(UoPacketWriter.AccountLogin(_username, _password), cancellationToken);

        var pending = new List<byte>(8192);
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read <= 0)
            {
                throw new IOException("Login socket closed before redirect packet.");
            }

            Append(pending, buffer, read);

            while (TryExtractRawPacket(pending, out var packet))
            {
                var opcode = packet[0];

                if (opcode == 0xA8)
                {
                    await stream.WriteAsync(UoPacketWriter.ServerSelect(0), cancellationToken);
                }

                if (opcode == 0x8C)
                {
                    return ParseRedirect(packet);
                }
            }
        }

        throw new OperationCanceledException();
    }

    private static bool TryExtractRawPacket(List<byte> pending, out byte[] packet)
    {
        packet = Array.Empty<byte>();

        if (pending.Count == 0)
        {
            return false;
        }

        var opcode = pending[0];
        int requiredLength;

        switch (opcode)
        {
            case 0x8C:
                requiredLength = 11;

                break;
            case 0xA8:
                if (pending.Count < 3)
                {
                    return false;
                }

                requiredLength = BinaryPrimitives.ReadUInt16BigEndian(new[] { pending[1], pending[2] });

                break;
            default:
                if (pending.Count < 3)
                {
                    return false;
                }

                requiredLength = BinaryPrimitives.ReadUInt16BigEndian(new[] { pending[1], pending[2] });

                break;
        }

        if (requiredLength <= 0 || pending.Count < requiredLength)
        {
            return false;
        }

        packet = pending.Take(requiredLength).ToArray();
        pending.RemoveRange(0, requiredLength);

        return true;
    }
}
