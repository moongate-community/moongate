using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;

namespace Moongate.Tests.TestSupport.Plugins;

public sealed class RecordingPlugin : IMoongatePlugin
{
    private readonly MoongatePluginData _metadata;
    private readonly Action<Container> _register;

    public int RegisterCalls { get; private set; }
    public int MetadataReads { get; private set; }

    public MoongatePluginData Metadata
    {
        get
        {
            MetadataReads++;
            return _metadata;
        }
    }

    public RecordingPlugin(MoongatePluginData metadata, Action<Container>? register = null)
    {
        _metadata = metadata;
        _register = register ?? (_ => { });
    }

    public void Register(Container container)
    {
        RegisterCalls++;
        _register(container);
    }
}
