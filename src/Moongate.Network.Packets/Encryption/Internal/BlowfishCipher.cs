// POL protocol port; see THIRD-PARTY-NOTICES.md for origin and license.
using System.Buffers.Binary;

namespace Moongate.Network.Packets.Encryption.Internal;

internal sealed class BlowfishCipher
{
    private static readonly (uint[] P, uint[] S)[] Tables = CreateTables();
    private readonly byte[] _feedback = new byte[8];
    private int _table = 1;
    private int _blockPosition;
    private int _streamPosition;

    internal BlowfishCipher()
    {
        SetSeed(0);
    }

    internal void Decrypt(Span<byte> data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (_streamPosition == 21036)
            {
                _table = (_table + 3) % 11;
                SetSeed(1);
                _streamPosition = 0;
                _blockPosition = 0;
            }
            if (_blockPosition == 0)
            {
                var left = BinaryPrimitives.ReadUInt32BigEndian(_feedback);
                var right = BinaryPrimitives.ReadUInt32BigEndian(_feedback.AsSpan(4));
                EncryptBlock(ref left, ref right, Tables[_table]);
                BinaryPrimitives.WriteUInt32BigEndian(_feedback, left);
                BinaryPrimitives.WriteUInt32BigEndian(_feedback.AsSpan(4), right);
            }
            var cipherByte = data[i];
            data[i] ^= _feedback[_blockPosition];
            _feedback[_blockPosition] = cipherByte;
            _blockPosition = (_blockPosition + 1) & 7;
            _streamPosition++;
        }
    }

    private void SetSeed(int phase)
    {
        BlowfishTables.Seeds.AsSpan((phase * 25 + _table) * 16, 8).CopyTo(_feedback);
    }

    private static (uint[] P, uint[] S)[] CreateTables()
    {
        var tables = new (uint[] P, uint[] S)[25];
        for (var table = 0; table < tables.Length; table++)
        {
            var p = BlowfishTables.P.ToArray();
            var s = BlowfishTables.S.ToArray();
            var keyPosition = 0;
            for (var i = 0; i < p.Length; i++)
            {
                uint mask = 0;
                for (var j = 0; j < 4; j++)
                {
                    mask = (mask << 8) | BlowfishTables.Keys[table * 6 + keyPosition];
                    keyPosition = (keyPosition + 1) % 6;
                }
                p[i] ^= mask;
            }
            tables[table] = (p, s);
            uint left = 0, right = 0;
            for (var i = 0; i < p.Length; i += 2)
            {
                EncryptBlock(ref left, ref right, tables[table]);
                p[i] = left;
                p[i + 1] = right;
            }
            for (var i = 0; i < s.Length; i += 2)
            {
                EncryptBlock(ref left, ref right, tables[table]);
                s[i] = left;
                s[i + 1] = right;
            }
        }
        return tables;
    }

    private static void EncryptBlock(ref uint left, ref uint right, (uint[] P, uint[] S) table)
    {
        var (p, s) = table;
        left ^= p[0];
        for (var i = 1; i < 16; i += 2)
        {
            right ^= p[i] ^ F(left, s);
            left ^= p[i + 1] ^ F(right, s);
        }
        right ^= p[17];
        (left, right) = (right, left);
    }

    private static uint F(uint value, uint[] s)
    {
        return unchecked(((s[value >> 24] + s[256 + ((value >> 16) & 255)]) ^
                          s[512 + ((value >> 8) & 255)]) + s[768 + (value & 255)]);
    }
}
