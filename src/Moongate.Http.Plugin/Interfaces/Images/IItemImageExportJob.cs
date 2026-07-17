using Moongate.Http.Plugin.Data;

namespace Moongate.Http.Plugin.Interfaces.Images;

/// <summary>Fills the item image cache in the background, one export at a time.</summary>
public interface IItemImageExportJob
{
    /// <summary>How far the current or last export has got.</summary>
    ItemImageExportStatus Status { get; }

    /// <summary>Starts an export. False when one is already running.</summary>
    bool TryStart();
}
