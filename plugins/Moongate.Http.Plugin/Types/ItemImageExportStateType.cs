namespace Moongate.Http.Plugin.Types;

/// <summary>Where a bulk item-image export stands.</summary>
public enum ItemImageExportStateType
{
    /// <summary>No export has run since the server started.</summary>
    Idle,

    /// <summary>An export is working through the item ids.</summary>
    Running,

    /// <summary>The last export finished; Failed says whether every item made it.</summary>
    Completed,

    /// <summary>The last export stopped early. The reason is in the log.</summary>
    Failed
}
