using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Internal.Items;

/// <summary>
///     The item a player picked up and has not dropped yet. Picking it up does not move it: it stays in its container
///     until the drop.
/// </summary>
public sealed record HeldItem(Serial Item);
