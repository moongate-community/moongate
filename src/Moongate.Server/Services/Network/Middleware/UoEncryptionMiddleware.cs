using System.Buffers.Binary;
using Moongate.Network.Client;
using Moongate.Network.Interfaces.Middleware;
using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Encryption;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Types.Encryption;
using Moongate.Server.Types.Network;

namespace Moongate.Server.Services.Network.Middleware;

/// <summary>
///     Negotiates POL encryption before packet framing. Each connection must own a fresh instance.
///     Register after compression so outgoing bytes are compressed before encryption.
/// </summary>
public sealed class UoEncryptionMiddleware : INetMiddleware
{
    private const int HandshakeBufferLength = 86;
    private const byte EncryptedSeedMarker = 0xEF;
    private const int ExtendedSeedLength = 21;
    private const int ClassicSeedLength = 4;
    private const int GameLoginLength = 65;
    private const int AccountLoginLength = 62;

    private readonly NetworkEncryptionMode _mode;
    private readonly UoEncryptionProfile _profile;
    private readonly bool _gameConnection;
    private readonly byte[] _handshake = new byte[HandshakeBufferLength];
    private int _buffered;
    private int _seedLength;
    private uint _seed;
    private bool _established;
    private bool _failed;
    private UoLoginCipher? _loginCipher;
    private UoGameCipher? _gameCipher;

    public UoEncryptionMiddleware(NetworkEncryptionMode mode, UoEncryptionProfile profile, bool gameConnection)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (mode != NetworkEncryptionMode.Disabled && profile.Type == UoEncryptionType.None)
        {
            throw new ArgumentException("Enabled encryption requires an encrypted profile.", nameof(profile));
        }

        _mode = mode;
        _profile = profile;
        _gameConnection = gameConnection;
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ProcessAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_failed)
        {
            throw new InvalidDataException("The UO encryption handshake has failed.");
        }

        if (_mode == NetworkEncryptionMode.Disabled || data.IsEmpty)
        {
            return ValueTask.FromResult(data);
        }

        try
        {
            return ValueTask.FromResult(ProcessReceived(data));
        }
        catch
        {
            _failed = true;
            Array.Clear(_handshake);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> ProcessSendAsync(
        MoongateTcpClient? client,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cipher = Volatile.Read(ref _gameCipher);
        if (cipher is null || data.IsEmpty)
        {
            return ValueTask.FromResult(data);
        }

        var result = data.ToArray();
        cipher.Encrypt(result);
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(result);
    }

    private ReadOnlyMemory<byte> ProcessReceived(ReadOnlyMemory<byte> data)
    {
        if (_established)
        {
            if (_loginCipher is null && _gameCipher is null)
            {
                return data;
            }

            var transformed = data.ToArray();
            Decrypt(transformed);
            return transformed;
        }

        var remaining = data.Span;
        if (_buffered == 0)
        {
            _seedLength = remaining[0] == EncryptedSeedMarker ? ExtendedSeedLength : ClassicSeedLength;
        }

        if (_buffered < _seedLength)
        {
            BufferUntil(ref remaining, _seedLength);
            if (_buffered < _seedLength)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            _seed = BinaryPrimitives.ReadUInt32BigEndian(_handshake.AsSpan(_seedLength == ExtendedSeedLength ? 1 : 0));
            if (_seed == 0)
            {
                throw new InvalidDataException("A UO connection cannot use a zero encryption seed.");
            }

            if (_seed == uint.MaxValue)
            {
                throw new InvalidDataException(
                    "The legacy KR encryption handshake is not supported by POL stream encryption."
                );
            }
        }

        var loginLength = _gameConnection ? GameLoginLength : AccountLoginLength;
        BufferUntil(ref remaining, _seedLength + loginLength);
        if (_buffered < _seedLength + loginLength)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var login = _handshake.AsSpan(_seedLength, loginLength);
        if (IsValidLogin(login))
        {
            if (_mode == NetworkEncryptionMode.Required)
            {
                throw new InvalidDataException("A plaintext client was rejected by the required encryption policy.");
            }
        }
        else
        {
            if (_gameConnection)
            {
                var cipher = new UoGameCipher(_seed, _profile.Type);
                cipher.Decrypt(login);
                RequireValidLogin(login);
                // Publish fully initialized state before decrypted frames can reach a handler and trigger sends.
                Volatile.Write(ref _gameCipher, cipher);
            }
            else
            {
                var cipher = new UoLoginCipher(_seed, _profile);
                cipher.Transform(login);
                RequireValidLogin(login);
                _loginCipher = cipher;
            }
        }

        var outputSeedLength = !_gameConnection && _seedLength == ClassicSeedLength ? ExtendedSeedLength : _seedLength;
        var result = new byte[outputSeedLength + loginLength + remaining.Length];
        if (outputSeedLength != _seedLength)
        {
            WriteVersionedSeed(result);
        }
        else
        {
            _handshake.AsSpan(0, _seedLength).CopyTo(result);
        }

        login.CopyTo(result.AsSpan(outputSeedLength));
        remaining.CopyTo(result.AsSpan(outputSeedLength + loginLength));
        Decrypt(result.AsSpan(outputSeedLength + loginLength));
        _established = true;
        Array.Clear(_handshake);
        return result;
    }

    private void BufferUntil(ref ReadOnlySpan<byte> data, int length)
    {
        var count = Math.Min(length - _buffered, data.Length);
        data[..count].CopyTo(_handshake.AsSpan(_buffered));
        _buffered += count;
        data = data[count..];
    }

    private void Decrypt(Span<byte> data)
    {
        _loginCipher?.Transform(data);
        _gameCipher?.Decrypt(data);
    }

    private bool IsValidLogin(ReadOnlySpan<byte> data)
    {
        // Reuse the packet contract: full-width ASCII and padding after NUL are valid; malformed credentials are not.
        return _gameConnection
            ? GameLoginPacket.TryParse(data, out _)
            : AccountLoginPacket.TryParse(data, out _);
    }

    private void RequireValidLogin(ReadOnlySpan<byte> data)
    {
        if (!IsValidLogin(data))
        {
            throw new InvalidDataException("The client handshake does not match the configured POL encryption profile.");
        }
    }

    private void WriteVersionedSeed(Span<byte> destination)
    {
        destination[0] = EncryptedSeedMarker;
        BinaryPrimitives.WriteUInt32BigEndian(destination[1..], _seed);
        BinaryPrimitives.WriteUInt32BigEndian(destination[5..], (uint)_profile.Major);
        BinaryPrimitives.WriteUInt32BigEndian(destination[9..], (uint)_profile.Minor);
        BinaryPrimitives.WriteUInt32BigEndian(destination[13..], (uint)_profile.Revision);
        BinaryPrimitives.WriteUInt32BigEndian(destination[17..], (uint)_profile.Patch);
    }
}
