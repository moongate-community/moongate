using DryIoc;
using Moongate.Abstractions.Interfaces.Services.Email;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Messaging;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Accounting;
using Moongate.Server.Services.Characters;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Lifecycle;
using Moongate.Server.Services.Messaging;
using Moongate.Server.Services.Movement;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Spatial;
using Moongate.Server.Services.Speech;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.World;
using Moongate.Email.Data;
using Moongate.Email.Services;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.UO.Data.Interfaces.Names;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Art;
using Moongate.UO.Data.Services.Names;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.Server.Extensions.Bootstrap;

/// <summary>
/// Registers core non-hosted services required by server runtime.
/// </summary>
public static class AddBootstrapCoreServicesExtension
{
    /// <summary>
    /// Registers base messaging, dispatch, session and utility services.
    /// </summary>
    public static Container AddBootstrapCoreServices(this Container container)
    {
        container.Register<IMessageBusService, MessageBusService>(Reuse.Singleton);
        container.Register<IBackgroundJobService, BackgroundJobService>(Reuse.Singleton);
        container.Register<IGameEventBusService, GameEventBusService>(Reuse.Singleton);
        container.Register<IDispatchEventsService, DispatchEventsService>(Reuse.Singleton);
        container.Register<IServerLifetimeService, ServerLifetimeService>(Reuse.Singleton);
        container.Register<IMovementTileQueryService, MovementTileQueryService>(Reuse.Singleton);
        container.Register<IMovementValidationService, MovementValidationService>(Reuse.Singleton);
        container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
        container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
        container.Register<IMobileService, MobileService>(Reuse.Singleton);
        container.Register<IStarterItemFactoryService, StarterItemFactoryService>(Reuse.Singleton);
        container.Register<IPlaceholderResolverService, PlaceholderResolverService>(Reuse.Singleton);
        container.Register<IStartupCompositionService, StartupCompositionService>(Reuse.Singleton);
        container.Register<IEntityFactoryService, EntityFactoryService>(Reuse.Singleton);
        container.Register<IOutgoingPacketQueue, OutgoingPacketQueue>(Reuse.Singleton);
        container.Register<IOutboundPacketSender, OutboundPacketSender>(Reuse.Singleton);
        container.Register<IPacketDispatchService, PacketDispatchService>(Reuse.Singleton);
        container.Register<IGameNetworkSessionService, GameNetworkSessionService>(Reuse.Singleton);
        container.Register<ISpatialWorldService, SpatialWorldService>(Reuse.Singleton);
        container.Register<ISpeechService, SpeechService>(Reuse.Singleton);
        container.Register<ITimerService, TimerWheelService>(Reuse.Singleton);
        container.Register<IAccountService, AccountService>(Reuse.Singleton);
        container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
        container.Register<IItemService, ItemService>(Reuse.Singleton);
        container.Register<IPlayerDragService, PlayerDragService>(Reuse.Singleton);
        container.Register<IItemScriptDispatcher, ItemScriptDispatcher>(Reuse.Singleton);
        container.Register<ILuaBrainRegistry, LuaBrainRegistry>(Reuse.Singleton);
        container.Register<INameService, NameService>(Reuse.Singleton);
        container.RegisterDelegate<IArtService>(_ => new ArtService(), Reuse.Singleton);
        container.Register<IItemTemplateService, ItemTemplateService>(Reuse.Singleton);
        container.Register<IMobileTemplateService, MobileTemplateService>(Reuse.Singleton);
        container.Register<IStartupTemplateService, StartupTemplateService>(Reuse.Singleton);
        container.Register<IWorldGeneratorBuilderService, WorldGeneratorBuilderService>(Reuse.Singleton);
        container.Register<IWorldGenerator, DoorGeneratorBuilder>(Reuse.Singleton);
        container.Register<IWorldGenerator, ItemsImageBuilder>(Reuse.Singleton);
        container.Register<ILocationCatalogService, LocationCatalogService>(Reuse.Singleton);
        container.Register<IDecorationDataService, DecorationDataService>(Reuse.Singleton);
        container.Register<ISignDataService, SignDataService>(Reuse.Singleton);
        container.Register<ISeedDataService, SeedDataService>(Reuse.Singleton);
        container.RegisterDelegate(
            resolver =>
            {
                var config = resolver.Resolve<MoongateConfig>();
                var directoriesConfig = resolver.Resolve<DirectoriesConfig>();
                var templatesPath = directoriesConfig[DirectoryType.EmailTemplates];

                return new EmailTemplateOptions
                {
                    TemplatesRootPath = templatesPath,
                    FallbackLocale = string.IsNullOrWhiteSpace(config.Email.FallbackLocale)
                                         ? "en"
                                         : config.Email.FallbackLocale,
                    WebsiteUrl = string.IsNullOrWhiteSpace(config.Http.WebsiteUrl)
                                     ? "http://localhost"
                                     : config.Http.WebsiteUrl
                };
            },
            Reuse.Singleton
        );
        container.Register<IEmailTemplateService, ScribanEmailTemplateService>(Reuse.Singleton);
        container.RegisterDelegate<IEmailSender>(
            resolver =>
            {
                var config = resolver.Resolve<MoongateConfig>();

                if (!config.Email.IsEnabled)
                {
                    return new NoOpEmailSender();
                }

                var smtpOptions = new SmtpEmailSenderOptions
                {
                    Host = config.Email.Smtp.Host,
                    Port = config.Email.Smtp.Port,
                    UseSsl = config.Email.Smtp.UseSsl,
                    Username = config.Email.Smtp.Username,
                    Password = config.Email.Smtp.Password
                };

                return new SmtpEmailSender(smtpOptions);
            },
            Reuse.Singleton
        );
        container.Register<IEmailService, EmailService>(Reuse.Singleton);

        return container;
    }
}
