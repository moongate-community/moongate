using System.Xml.Linq;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Handlers.General;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Handlers.General;

public sealed class HelpRequestPacketHandlerTests : IAsyncLifetime
{
    private readonly RecordingGumpService _gumps = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
    }

    [Fact]
    public void Handle_OpensTheHelpMenuOfTheCharacter()
    {
        new HelpRequestPacketHandler(_fixture.Mobiles, GumpModule()).Handle(_session, new HelpRequestPacket());

        Assert.Equal("help", Assert.Single(_gumps.Opened).Gump.Layout.Build().Strings[0]);
    }

    [Fact]
    public void Handle_WithoutACharacter_OpensNothing()
    {
        var stranger = _fixture.Sessions.GetOrCreate(new ControlledNetworkConnection(77));

        new HelpRequestPacketHandler(_fixture.Mobiles, GumpModule()).Handle(stranger, new HelpRequestPacket());

        Assert.Empty(_gumps.Opened);
    }

    [Fact]
    public void Handle_WithoutTheGumpModule_DoesNotFail()
    {
        new HelpRequestPacketHandler(_fixture.Mobiles).Handle(_session, new HelpRequestPacket());

        Assert.Empty(_gumps.Opened);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    private GumpModule GumpModule()
    {
        var loop = new StubGameLoop();
        var template = new GumpTemplate
        {
            Id = "help_menu", File = "help_menu.xml",
            Root = XElement.Parse("""<gump id="help_menu"><text x="1" y="1">help</text></gump>""")
        };
        var templates = new GumpTemplateService(_gumps, new StubDataLoaderService().With(template), loop, _fixture.Sessions);

        return new(_fixture.Sessions, _gumps, templates, new(new RecordingGumpScriptService()), loop);
    }
}
