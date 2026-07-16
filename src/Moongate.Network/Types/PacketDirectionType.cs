namespace Moongate.Network.Types;

/// <summary>Direction of a UO packet relative to the server.</summary>
[Flags]
public enum PacketDirectionType : byte
{
    None = 0,
    Input = 1,
    Output = 2
}
