// POL protocol port; see THIRD-PARTY-NOTICES.md for origin and license.
using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Types.Encryption;

namespace Moongate.Network.Packets.Encryption;

/// <summary>Stateful client-to-server POL login stream cipher.</summary>
public sealed class UoLoginCipher
{
    private readonly UoEncryptionProfile _profile;
    private uint _low;
    private uint _high;

    public UoLoginCipher(uint seed, UoEncryptionProfile profile)
    {
        _profile = profile;
        _low = ((~seed ^ 0x00001357) << 16) | ((seed ^ 0xFFFFAAAA) & 0xFFFF);
        _high = ((seed ^ 0x43210000) >> 16) | ((~seed ^ 0xABCDFFFF) & 0xFFFF0000);
    }

    /// <summary>Transforms bytes in place, preserving stream position between calls.</summary>
    public void Transform(Span<byte> data)
    {
        if (_profile.Type == UoEncryptionType.None)
        {
            return;
        }

        unchecked
        {
            for (var i = 0; i < data.Length; i++)
            {
                data[i] ^= (byte)_low;
                var low = _low;
                var high = _high;
                switch (_profile.Type)
                {
                    case UoEncryptionType.OldBlowfish:
                        _low = ((low >> 1) | (high << 31)) ^ _profile.Key2;
                        _high = ((high >> 1) | (low << 31)) ^ _profile.Key1;
                        break;
                    case UoEncryptionType.Blowfish12536:
                        // C# masks shifts to five bits, matching the original x86 client.
                        _high = (_profile.Key1 >> (int)(5 * high * high)) + high * _profile.Key1 +
                                low * low * 0x35CE9581 + 0x07AFCC37;
                        _low = (_profile.Key2 >> (int)(3 * low * low)) + low * _profile.Key2 -
                               _high * _high * 0x4C3A1353 + 0x16EF783F;
                        break;
                    default:
                        _high = (((((high >> 1) | (low << 31)) ^ _profile.Key1) >> 1) | (low << 31)) ^ _profile.Key1;
                        _low = ((low >> 1) | (high << 31)) ^ _profile.Key2;
                        break;
                }
            }
        }
    }
}
