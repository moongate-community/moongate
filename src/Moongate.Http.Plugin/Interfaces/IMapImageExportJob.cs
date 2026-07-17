using Moongate.Http.Plugin.Data;

namespace Moongate.Http.Plugin.Interfaces;

/// <summary>Builds every map tile and whole-facet image in the background, one run at a time.</summary>
public interface IMapImageExportJob
{
    /// <summary>How far the current or last pre-warm has got.</summary>
    MapImageExportStatus Status { get; }

    /// <summary>Starts a pre-warm. False when one is already running.</summary>
    bool TryStart();
}
