namespace Moongate.Server.Abstractions.Data.Plugins;

/// <summary>
/// One plugin the bootstrap activated. <see cref="AssemblyName" /> is what joins a plugin to the HTTP
/// routes it declares: an endpoint's handler names the type that declares it, and that type's assembly
/// is matched against this.
/// </summary>
public record PluginDescriptor(
    string Id,
    string Name,
    Version Version,
    string Author,
    string Description,
    string AssemblyName,
    bool IsExternal
);
