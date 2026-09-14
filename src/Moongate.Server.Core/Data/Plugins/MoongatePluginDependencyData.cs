namespace Moongate.Server.Core.Data.Plugins;

/// <summary>Declares a required plugin and an optional inclusive minimum version.</summary>
public sealed record MoongatePluginDependencyData
{
    public string Id { get; }
    public Version? MinimumVersion { get; }

    public MoongatePluginDependencyData(string id, Version? minimumVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        MinimumVersion = minimumVersion;
    }
}
