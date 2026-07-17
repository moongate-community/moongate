namespace Moongate.Http.Plugin.Data;

/// <summary>How far the map image pre-warm has got.</summary>
/// <param name="State">Idle, Running, Completed or Failed.</param>
/// <param name="Done">Tiles and whole-facet images produced so far.</param>
/// <param name="Total">How many there are to produce. 0 until the pre-warm has counted them.</param>
/// <param name="Failed">How many could not be produced. The reasons are in the log.</param>
/// <param name="StartedAt">When the current or last pre-warm started; null if none ever has.</param>
public record MapImageExportStatus(string State, int Done, int Total, int Failed, DateTimeOffset? StartedAt);
