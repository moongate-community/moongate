using Moongate.Http.Plugin.Data;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>Background warm-up of the body image cache over the classified bodies.</summary>
public interface IBodyImageExportJob
{
    /// <summary>Where the export stands right now.</summary>
    BodyImageExportStatus Status { get; }

    /// <summary>Starts an export unless one is already running; false when refused.</summary>
    bool TryStart();
}
