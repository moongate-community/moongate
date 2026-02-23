using BenchmarkDotNet.Attributes;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Registry;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
/// <summary>
/// Represents PacketParsingBenchmark.
/// </summary>
public class PacketParsingBenchmark
{
    private readonly PacketRegistry _registry = new();
    private readonly byte[] _loginSeedBuffer = new byte[21];

    [Benchmark]
    public bool ParseLoginSeedPacket()
    {
        if (!_registry.TryCreatePacket(0xEF, out var packet))
        {
            return false;
        }

        return packet is LoginSeedPacket && packet.TryParse(_loginSeedBuffer);
    }

    [GlobalSetup]
    public void Setup()
    {
        PacketTable.Register(_registry);

        _loginSeedBuffer[0] = 0xEF;
        _loginSeedBuffer[1] = 0x00;
        _loginSeedBuffer[2] = 0x00;
        _loginSeedBuffer[3] = 0x00;
        _loginSeedBuffer[4] = 0x01;
        _loginSeedBuffer[5] = 0x00;
        _loginSeedBuffer[6] = 0x00;
        _loginSeedBuffer[7] = 0x00;
        _loginSeedBuffer[8] = 0x07;
        _loginSeedBuffer[9] = 0x00;
        _loginSeedBuffer[10] = 0x00;
        _loginSeedBuffer[11] = 0x00;
        _loginSeedBuffer[12] = 0x00;
        _loginSeedBuffer[13] = 0x00;
        _loginSeedBuffer[14] = 0x00;
        _loginSeedBuffer[15] = 0x00;
        _loginSeedBuffer[16] = 0x00;
        _loginSeedBuffer[17] = 0x00;
        _loginSeedBuffer[18] = 0x00;
        _loginSeedBuffer[19] = 0x00;
        _loginSeedBuffer[20] = 0x72;
    }
}
