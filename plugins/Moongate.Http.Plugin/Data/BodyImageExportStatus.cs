namespace Moongate.Http.Plugin.Data;

/// <summary>How far the bulk body-image export has got.</summary>
/// <param name="State">Idle, Running, Completed or Failed.</param>
/// <param name="Done">Bodies processed so far.</param>
/// <param name="Total">Classified, non-equipment bodies to process. 0 until the export has counted them.</param>
/// <param name="Failed">Bodies whose frame could not be rendered or written. The reasons are in the log.</param>
/// <param name="StartedAt">When the current or last export started; null if none ever has.</param>
public record BodyImageExportStatus(string State, int Done, int Total, int Failed, DateTimeOffset? StartedAt);
