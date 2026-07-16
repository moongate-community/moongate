using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.UO.Data.Hues;

namespace Moongate.Network.Data;

/// <summary>
/// One item inside a container as it appears in a container content (0x3C) packet: the serial, graphic,
/// stack size, the slot it sits in within the container gump, and its hue.
/// </summary>
public readonly record struct ContainerItem(Serial Serial, ushort ItemId, ushort Amount, Point2D Position, Hue Hue);
