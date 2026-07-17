namespace Moongate.Http.Plugin.Types;

/// <summary>Where a bulk body-image export stands.</summary>
public enum BodyImageExportStateType
{
    /// <summary>No export has run since the server started.</summary>
    Idle,

    /// <summary>An export is working through the classified bodies.</summary>
    Running,

    /// <summary>The last export finished; Failed says whether every body made it.</summary>
    Completed,

    /// <summary>The last export stopped early. The reason is in the log.</summary>
    Failed
}
