namespace Moongate.Http.Plugin.Types;

/// <summary>Where a map image pre-warm stands.</summary>
public enum MapImageExportStateType
{
    /// <summary>No pre-warm has run since the server started.</summary>
    Idle,

    /// <summary>A pre-warm is working through the facets.</summary>
    Running,

    /// <summary>The last pre-warm finished; Failed says whether every tile made it.</summary>
    Completed,

    /// <summary>The last pre-warm stopped early. The reason is in the log.</summary>
    Failed
}
