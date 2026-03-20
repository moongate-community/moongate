using System.Reflection;
using Moongate.Plugin.Abstractions.Interfaces;

namespace Moongate.Server.Data.Internal.Plugins;

/// <summary>
/// Represents a discovered plugin after its entry type has been loaded.
/// </summary>
internal sealed record class LoadedPlugin(
    DiscoveredPlugin DiscoveredPlugin,
    Assembly Assembly,
    Type EntryType,
    IMoongatePlugin Instance
);
