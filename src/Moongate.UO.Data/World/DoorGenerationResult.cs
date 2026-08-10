namespace Moongate.UO.Data.World;

/// <summary>What a run of the door generator did.</summary>
/// <param name="Placed">Doorways that had no door and now have one.</param>
/// <param name="Skipped">Doorways that already held a door with that graphic.</param>
/// <param name="Scanned">Tiles read, so the cost of the run is reportable rather than guessed at.</param>
public readonly record struct DoorGenerationResult(int Placed, int Skipped, long Scanned);
