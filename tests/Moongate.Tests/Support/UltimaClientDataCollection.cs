namespace Moongate.Tests.Support;

/// <summary>
/// Serializes tests that mutate the process-wide Moongate.Ultima static state
/// (Files.SetDirectory, TileData/Hues re-initialization). Parallel execution of
/// these tests would race on the shared client-file paths.
/// </summary>
[CollectionDefinition("UltimaClientData", DisableParallelization = true)]
public sealed class UltimaClientDataCollection
{
}
