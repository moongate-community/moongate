using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookServicesRegistrationTests
{
    [Fact]
    public async Task RegisteredWorldServices_ResolveBookModuleAndDocumentService()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        using var container = new Container();
        container.RegisterMoongatePersistence(new());
        container.AddUltimaWorldServices();
        container.RegisterInstance<IDataLoaderService>(fixture.Data, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemService>(fixture.Items, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IMobileService>(fixture.World.Mobiles, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemHandlingService>(fixture.Handling, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemTemplateService>(fixture.ItemTemplates, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<ISessionService>(fixture.World.Sessions);
        container.RegisterInstance<IBankService>(fixture.Bank, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IGumpService>(fixture.Gumps, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IGameLoopService>(fixture.World.Network.Loop);
        container.RegisterInstance<IScriptEngine>(fixture.Engine);
        container.RegisterInstance(fixture.Contexts, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance(new LocalizationConfig());
        Assert.IsType<BookTemplateService>(container.Resolve<IBookTemplateService>());
        Assert.IsType<BookDocumentService>(container.Resolve<IBookDocumentService>());
        Assert.Contains(typeof(BookModule), container.Resolve<IScriptModuleRegistry>().ModuleTypes);
        Assert.NotNull(container.Resolve<BookModule>());
    }
}
