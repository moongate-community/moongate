namespace Moongate.Server.Data.World;

/// <summary>
/// Represents one row from data/components/doors.txt.
/// </summary>
public readonly record struct DoorComponentEntry(
    int Category,
    int Piece1,
    int Piece2,
    int Piece3,
    int Piece4,
    int Piece5,
    int Piece6,
    int Piece7,
    int Piece8,
    int FeatureMask,
    string Comment
);
