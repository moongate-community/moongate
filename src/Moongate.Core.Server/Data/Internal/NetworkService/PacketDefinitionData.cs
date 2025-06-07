using Moongate.Core.Server.Interfaces.Packets;

namespace Moongate.Core.Server.Data.Internal.NetworkService;

public record struct PacketDefinitionData(int OpCode, int Length, string Description);

