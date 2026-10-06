using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Config;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
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
        using var root = new TemporaryDirectory();
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["data", "templates"]));
        container.RegisterInstance(TestConfigDocuments.Empty(root.Path));
        container.RegisterInstance(ServerMode.Game);
        new MoongateUltimaPlugin().Register(container);
        container.RegisterInstance<IDataLoaderService>(fixture.Data, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemService>(fixture.Items, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IMobileService>(fixture.World.Mobiles, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemHandlingService>(fixture.Handling, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IItemTemplateService>(
            fixture.ItemTemplates,
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<ISessionService>(fixture.World.Sessions);
        container.RegisterInstance<IBankService>(fixture.Bank, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IGumpService>(fixture.Gumps, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance<IGameLoopService>(fixture.World.Network.Loop);
        container.RegisterInstance<IScriptEngine>(fixture.Engine);
        container.RegisterInstance(fixture.Contexts, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance(new LocalizationConfig(), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        Assert.IsType<BookTemplateService>(container.Resolve<IBookTemplateService>());
        Assert.IsType<BookDocumentService>(container.Resolve<IBookDocumentService>());
        Assert.Contains(typeof(BookModule), container.Resolve<IScriptModuleRegistry>().ModuleTypes);
        Assert.NotNull(container.Resolve<BookModule>());
        container.RegisterInstance<IDataAccess<JailSentenceEntity>>(
            new RecordingDataAccess<JailSentenceEntity>(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<IDataAccess<MobileEntity>>(
            new RecordingDataAccess<MobileEntity>(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<IDataAccess<AccountEntity>>(
            new RecordingDataAccess<AccountEntity>(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<ITeleportService>(
            new RecordingTeleportService(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<ISpeechService>(
            new RecordingSpeechService(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<IWorldViewService>(
            new RecordingWorldViewService(),
            ifAlreadyRegistered: IfAlreadyRegistered.Replace
        );
        container.RegisterInstance<ITimerService>(new RecordingTimerService());
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterInstance(TestLocalization.With(), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        Assert.IsType<JailService>(container.Resolve<IJailService>());
    }
}
