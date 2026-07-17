using SquidStd.Core.Interfaces.Config;

namespace Moongate.Tests.Support;

/// <summary>
/// Stands in for the real config manager, recording saves instead of writing moongate.yaml. Only
/// <see cref="Save" /> is exercised; the rest would be a lie if it pretended to work.
/// </summary>
public sealed class StubConfigManagerService : IConfigManagerService
{
    public int SaveCount { get; private set; }

    public string ConfigName => "moongate";

    public string ConfigDirectory => string.Empty;

    public string ConfigPath => string.Empty;

    public IReadOnlyCollection<IConfigEntry> Entries => [];

    public event Action? ConfigLoaded;

    public string Compose()
        => throw new NotSupportedException();

    public TConfig GetConfig<TConfig>()
        where TConfig : class
        => throw new NotSupportedException();

    public void Load()
        => throw new NotSupportedException();

    public void Save()
        => SaveCount++;
}
