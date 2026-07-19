using DryIoc;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Extensions;
using Moongate.Server.Loaders;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Loading;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server;

public class MoongateDataLoaderPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.dataloaders.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateDataLoaderPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Data Loaders",
            Description = "Moongate data loaders plugin"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        // Priority 100 so it starts after the event bus and the Lua forwarder are up,
        // ensuring subscribers actually receive the FilesLoadedEvent.
        container.RegisterStdService<FilesLoaderService, FilesLoaderService>(100);

        container.Register<ISkillService, SkillService>(Reuse.Singleton);
        container.RegisterDataLoader<SkillLoader>();

        container.Register<IProfessionService, ProfessionService>(Reuse.Singleton);
        container.RegisterDataLoader<ProfessionsLoader>(10);

        container.Register<ILocationService, LocationService>(Reuse.Singleton);
        container.RegisterDataLoader<LocationsLoader>(20);

        container.Register<INameService, NameService>(Reuse.Singleton);
        container.RegisterDataLoader<NamesLoader>(30);

        container.Register<IRegionService, RegionService>(Reuse.Singleton);
        container.RegisterDataLoader<RegionsLoader>(40);

        container.Register<IWeatherService, WeatherService>(Reuse.Singleton);
        container.RegisterDataLoader<WeatherLoader>(50);

        container.Register<ITeleporterService, TeleporterService>(Reuse.Singleton);
        container.RegisterDataLoader<TeleportersLoader>(60);

        container.Register<IContainerService, ContainerService>(Reuse.Singleton);
        container.RegisterDataLoader<ContainersLoader>(70);

        container.Register<ISignService, SignService>(Reuse.Singleton);
        container.RegisterDataLoader<SignsLoader>(80);

        container.Register<IContainerGumpService, ContainerGumpService>(Reuse.Singleton);
        container.RegisterDataLoader<ContainerGumpsLoader>(90);

        container.Register<IStartingCityService, StartingCityService>(Reuse.Singleton);
        container.RegisterDataLoader<StartingCitiesLoader>(100);

        container.Register<ITitleService, TitleService>(Reuse.Singleton);
        container.RegisterDataLoader<TitlesLoader>(110);

        container.Register<IItemTemplateService, ItemTemplateService>(Reuse.Singleton);
        container.RegisterDataLoader<ItemTemplatesLoader>(120);

        container.Register<ILootTemplateService, LootTemplateService>(Reuse.Singleton);
        container.RegisterDataLoader<LootTemplatesLoader>(130);

        container.Register<IStartingItemsService, StartingItemsService>(Reuse.Singleton);
        container.RegisterDataLoader<StartingItemsLoader>(140);

        container.Register<IMobileTemplateService, MobileTemplateService>(Reuse.Singleton);
        container.RegisterDataLoader<MobileTemplatesLoader>(150);

        container.RegisterDataLoaderService();
    }
}
