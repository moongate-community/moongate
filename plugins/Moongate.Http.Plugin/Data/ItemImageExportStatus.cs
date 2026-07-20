namespace Moongate.Http.Plugin.Data;

/// <summary>How far the bulk item-image export has got.</summary>
/// <param name="State">Idle, Running, Completed or Failed.</param>
/// <param name="Done">Items exported so far.</param>
/// <param name="Total">Items with art to export. 0 until the export has counted them.</param>
/// <param name="Failed">Items whose art could not be written. The reasons are in the log.</param>
/// <param name="StartedAt">When the current or last export started; null if none ever has.</param>
public record ItemImageExportStatus(string State, int Done, int Total, int Failed, DateTimeOffset? StartedAt);
