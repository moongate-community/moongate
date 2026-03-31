using DryIoc;
using Moongate.Abstractions.Interfaces.Services.Email;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Email.Data;
using Moongate.Email.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Characters;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Lifecycle;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Messaging;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Accounting;
using Moongate.Server.Services.Characters;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Lifecycle;
using Moongate.Server.Services.Maps;
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Messaging;
using Moongate.Server.Services.Movement;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Scripting.Jobs;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Spatial;
using Moongate.Server.Services.Speech;
using Moongate.Server.Services.Timing;
using Moongate.Server.Services.World;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Maps;
using Moongate.UO.Data.Interfaces.Names;
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
        container.Register<IAsyncWorkSchedulerService, AsyncWorkSchedulerService>(Reuse.Singleton);
        container.Register<IGameEventBusService, GameEventBusService>(Reuse.Singleton);
        container.Register<IDispatchEventsService, DispatchEventsService>(Reuse.Singleton);
        container.Register<IServerLifetimeService, ServerLifetimeService>(Reuse.Singleton);
        container.Register<IMovementTileQueryService, MovementTileQueryService>(Reuse.Singleton);
        container.Register<IMovementValidationService, MovementValidationService>(Reuse.Singleton);
        container.Register<IPathfindingService, AStarPathfindingService>(Reuse.Singleton);
        container.Register<IItemFactoryService, ItemFactoryService>(Reuse.Singleton);
        container.Register<IDoorService, DoorService>(Reuse.Singleton);
        container.Register<IDoorLockService, DoorLockService>(Reuse.Singleton);
        container.Register<IMobileFactoryService, MobileFactoryService>(Reuse.Singleton);
        container.Register<IMobileModifierAggregationService, MobileModifierAggregationService>(Reuse.Singleton);
        container.RegisterInstance(new MountTileData());
        container.Register<IMobileService, MobileService>(Reuse.Singleton);
        container.Register<IStartupLoadoutScriptService, StartupLoadoutScriptService>(Reuse.Singleton);
        container.Register<IEntityFactoryService, EntityFactoryService>(Reuse.Singleton);
        container.Register<IOutgoingPacketQueue, OutgoingPacketQueue>(Reuse.Singleton);
        container.Register<IOutboundPacketSender, OutboundPacketSender>(Reuse.Singleton);
        container.Register<IPacketDispatchService, PacketDispatchService>(Reuse.Singleton);
        container.Register<IGameNetworkSessionService, GameNetworkSessionService>(Reuse.Singleton);
        container.Register<IGameLoginHandoffService, GameLoginHandoffService>(Reuse.Singleton);
        container.Register<ISpatialWorldService, SpatialWorldService>(Reuse.Singleton);
        container.Register<ISpeechService, SpeechService>(Reuse.Singleton);
        container.Register<IChatSystemService, ChatSystemService>(Reuse.Singleton);
        container.Register<ITimerService, TimerWheelService>(Reuse.Singleton);
        container.RegisterDelegate(
            _ =>
            {
                var registry = new SpellRegistry();
                SpellInitializer.RegisterAll(registry);

                return registry;
            },
            Reuse.Singleton
        );
        container.Register<ISpellbookService, SpellbookService>(Reuse.Singleton);
        container.Register<IMagicService, MagicService>(Reuse.Singleton);
        container.Register<IAccountService, AccountService>(Reuse.Singleton);
        container.Register<ICharacterService, CharacterService>(Reuse.Singleton);
        container.Register<IPlayerLoginWorldSyncService, PlayerLoginWorldSyncService>(Reuse.Singleton);
        container.Register<IItemService, ItemService>(Reuse.Singleton);
        container.Register<IItemBookService, ItemBookService>(Reuse.Singleton);
        container.Register<ILootGenerationService, LootGenerationService>(Reuse.Singleton);
        container.Register<IItemInteractionService, ItemInteractionService>(Reuse.Singleton);
        container.Register<IItemManipulationService, ItemManipulationService>(Reuse.Singleton);
        container.Register<IPlayerDragService, PlayerDragService>(Reuse.Singleton);
        container.Register<IPlayerTargetService, PlayerTargetService>(Reuse.Singleton);
        container.Register<IDyeColorService, DyeColorService>(Reuse.Singleton);
        container.Register<IBulletinBoardService, BulletinBoardService>(Reuse.Singleton);
        container.Register<IContextMenuService, ContextMenuService>(Reuse.Singleton);
        container.Register<IHelpRequestService, HelpRequestService>(Reuse.Singleton);
        container.Register<IResurrectionOfferService, ResurrectionOfferService>(Reuse.Singleton);
        container.Register<IResurrectionService, ResurrectionService>(Reuse.Singleton);
        container.Register<IHelpTicketService, HelpTicketService>(Reuse.Singleton);
        container.Register<INotorietyService, NotorietyService>(Reuse.Singleton);
        container.Register<IAiRelationService, AiRelationService>(Reuse.Singleton);
        container.Register<ISkillAntiMacroService, SkillAntiMacroService>(Reuse.Singleton);
        container.Register<IStatGainService, StatGainService>(Reuse.Singleton);
        container.Register<ISkillGainService, SkillGainService>(Reuse.Singleton);
        container.RegisterDelegate<ICombatService>(
            resolver => new CombatService(
                resolver.Resolve<IMobileService>(),
                resolver.Resolve<IGameNetworkSessionService>(),
                resolver.Resolve<IOutgoingPacketQueue>(),
                resolver.Resolve<ITimerService>(),
                resolver.Resolve<ISpatialWorldService>(),
                resolver.Resolve<IGameEventBusService>(),
                resolver.Resolve<IItemService>(),
                resolver.Resolve<IDeathService>(),
                resolver.Resolve<ISkillGainService>()
            ),
            Reuse.Singleton
        );
        container.Register<IBandageService, BandageService>(Reuse.Singleton);
        container.Register<IDeathService, DeathService>(Reuse.Singleton);
        container.Register<IFameKarmaService, FameKarmaService>(Reuse.Singleton);
        container.Register<MobileCombatSoundResolver>(Reuse.Singleton);
        container.Register<IPlayerSellBuyService, PlayerSellBuyService>(Reuse.Singleton);
        container.Register<IItemScriptDispatcher, ItemScriptDispatcher>(Reuse.Singleton);
        container.Register<IGumpScriptDispatcherService, GumpScriptDispatcherService>(Reuse.Singleton);
        container.Register<ITextTemplateService, TextTemplateService>(Reuse.Singleton);
        container.Register<IBookTemplateService, BookTemplateService>(Reuse.Singleton);
        container.Register<INpcAiPromptService, NpcAiPromptService>(Reuse.Singleton);
        container.Register<INpcAiMemoryService, NpcAiMemoryService>(Reuse.Singleton);
        container.Register<IDialogueDefinitionService, DialogueDefinitionService>(Reuse.Singleton);
        container.Register<IDialogueMemoryService, DialogueMemoryService>(Reuse.Singleton);
        container.Register<IDialogueRuntimeService, DialogueRuntimeService>(Reuse.Singleton);
        container.Register<IScheduledEventDefinitionService, ScheduledEventDefinitionService>(Reuse.Singleton);
        container.Register<INpcAiRuntimeStateService, NpcAiRuntimeStateService>(Reuse.Singleton);
        container.Register<IOpenAiNpcDialogueClient, OpenAiNpcDialogueClient>(Reuse.Singleton);
        container.Register<INpcDialogueService, NpcDialogueService>(Reuse.Singleton);
        container.Register<AsyncLuaValueConverter>(Reuse.Singleton);
        container.Register<EchoAsyncLuaJobHandler>(Reuse.Singleton);
        container.RegisterDelegate<IAsyncLuaJobRegistry>(
            resolver =>
            {
                var registry = new AsyncLuaJobRegistry();
                _ = registry.TryRegister(resolver.Resolve<EchoAsyncLuaJobHandler>());

                return registry;
            },
            Reuse.Singleton
        );
        container.Register<IAsyncLuaJobService, AsyncLuaJobService>(Reuse.Singleton);
        container.Register<ILuaBrainRegistry, LuaBrainRegistry>(Reuse.Singleton);
        container.Register<INameService, NameService>(Reuse.Singleton);
        container.RegisterDelegate<IArtService>(_ => new ArtService(), Reuse.Singleton);
        container.RegisterDelegate<IMapImageService>(_ => new MapImageService(), Reuse.Singleton);
        container.Register<IItemTemplateService, ItemTemplateService>(Reuse.Singleton);
        container.Register<ILootTemplateService, LootTemplateService>(Reuse.Singleton);
        container.Register<IMobileTemplateService, MobileTemplateService>(Reuse.Singleton);
        container.Register<IQuestTemplateService, QuestTemplateService>(Reuse.Singleton);
        container.Register<IFactionTemplateService, FactionTemplateService>(Reuse.Singleton);
        container.Register<ISellProfileTemplateService, SellProfileTemplateService>(Reuse.Singleton);
        container.Register<IWorldGeneratorBuilderService, WorldGeneratorBuilderService>(Reuse.Singleton);
        container.Register<IDoorGenerationMapSpecProvider, DefaultDoorGenerationMapSpecProvider>(Reuse.Singleton);
        container.Register<IWorldGenerator, DoorGeneratorBuilder>(Reuse.Singleton);
        container.Register<IWorldGenerator, ItemsImageBuilder>(Reuse.Singleton);
        container.Register<ILocationCatalogService, LocationCatalogService>(Reuse.Singleton);
        container.Register<IDecorationDataService, DecorationDataService>(Reuse.Singleton);
        container.Register<ISignDataService, SignDataService>(Reuse.Singleton);
        container.Register<ISpawnsDataService, SpawnsDataService>(Reuse.Singleton);
        container.Register<ITeleportersDataService, TeleportersDataService>(Reuse.Singleton);
        container.Register<IDoorDataService, DoorDataService>(Reuse.Singleton);
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
