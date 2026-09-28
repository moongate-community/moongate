using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Items;

/// <summary>
///     One item of a 0x3C container content, copied when the packet is built.
/// </summary>
public sealed record ContainerItemEntry(
    Serial Serial,
    int ItemId,
    int Amount,
    int GridX,
    int GridY,
    Serial Container,
    Hue Hue
);
