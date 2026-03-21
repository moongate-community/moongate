using Moongate.Network.Interfaces;

namespace Moongate.Tests.Network.Support;

public sealed class TestClientEncryption : IClientEncryption
{
    public void ClientDecrypt(Span<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] ^= 0xAA;
        }
    }

    public void ServerEncrypt(Span<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] ^= 0x55;
        }
    }
}
