namespace Moongate.Server.Ultima.Data.MapItems;

/// <summary>
///     The part of the world a map item shows: its north-west and south-east corners in tiles, the size of its drawing in
///     pixels and the client's id of the facet.
/// </summary>
public sealed record MapArea(int X1, int Y1, int X2, int Y2, int Width, int Height, int Facet);
