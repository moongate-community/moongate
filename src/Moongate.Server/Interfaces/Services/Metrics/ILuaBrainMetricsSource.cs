using Moongate.Server.Metrics.Data;

namespace Moongate.Server.Interfaces.Services.Metrics;

/// <summary>
/// Provides Lua brain runner metrics snapshots.
/// </summary>
public interface ILuaBrainMetricsSource
{
    /// <summary>
    /// Gets the latest Lua brain runner metrics snapshot.
    /// </summary>
    LuaBrainMetricsSnapshot GetMetricsSnapshot();
}
