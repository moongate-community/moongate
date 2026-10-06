// POL protocol port; see THIRD-PARTY-NOTICES.md for origin and license.

using System.Security.Cryptography;
using Moongate.Network.Packets.Encryption.Internal;
using Moongate.Network.Packets.Types.Encryption;

namespace Moongate.Network.Packets.Encryption;

/// <summary>
///     Independent inbound and outbound POL game stream state for one connection.
/// </summary>
public sealed class UoGameCipher
{
    private const int ReceiveTableLength = 256;
    private const int SendKeyPositionMask = 15;

    private readonly TwofishEngine? _twofish;
    private readonly BlowfishCipher? _blowfish;
    private readonly byte[]? _receiveTable;
    private readonly byte[]? _sendKey;
    private int _receivePosition;
    private int _sendPosition;

    public UoGameCipher(uint seed, UoEncryptionType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (type is UoEncryptionType.BlowfishTwofish or UoEncryptionType.Twofish)
        {
            _twofish = new(seed);
            _receiveTable = new byte[ReceiveTableLength];
            for (var i = 0; i < _receiveTable.Length; i++)
            {
                _receiveTable[i] = (byte)i;
            }

            _twofish.EncryptBlocks(_receiveTable);
            if (type == UoEncryptionType.Twofish)
            {
                // UO requires MD5 to derive its server-to-client XOR stream.
#pragma warning disable CA5351
                _sendKey = MD5.HashData(_receiveTable);
#pragma warning restore CA5351
            }
        }

        if (type is UoEncryptionType.OldBlowfish or UoEncryptionType.Blowfish12536 or
            UoEncryptionType.Blowfish or UoEncryptionType.BlowfishTwofish)
        {
            _blowfish = new();
        }
    }

    /// <summary>
    ///     Deciphers client bytes in place; call serially in receive order.
    /// </summary>
    public void Decrypt(Span<byte> data)
    {
        if (_twofish is not null && _receiveTable is { } receiveTable)
        {
            for (var i = 0; i < data.Length; i++)
            {
                if (_receivePosition == ReceiveTableLength)
                {
                    _twofish.EncryptBlocks(receiveTable);
                    _receivePosition = 0;
                }

                data[i] ^= receiveTable[_receivePosition++];
            }
        }

        _blowfish?.Decrypt(data);
    }

    /// <summary>
    ///     Enciphers compressed server bytes; safe concurrently with one receive caller.
    /// </summary>
    public void Encrypt(Span<byte> data)
    {
        if (_sendKey is null)
        {
            return;
        }

        for (var i = 0; i < data.Length; i++)
        {
            data[i] ^= _sendKey[_sendPosition];
            _sendPosition = (_sendPosition + 1) & SendKeyPositionMask;
        }
    }
}
