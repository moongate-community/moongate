using Moongate.Core.Data.Directories;
using Moongate.Server.Data.Config;
using Moongate.Server.FileLoaders;
using Moongate.Server.Services.Scripting;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.TemplateValidator.Data.Internal;

internal sealed class TemplateValidatorRuntimeContext
{
    public DirectoriesConfig DirectoriesConfig { get; }

    public MoongateConfig Config { get; }

    public ItemTemplateService ItemTemplateService { get; }

    public MobileTemplateService MobileTemplateService { get; }

    public LootTemplateService LootTemplateService { get; }

    public FactionTemplateService FactionTemplateService { get; }

    public SellProfileTemplateService SellProfileTemplateService { get; }

    public BookTemplateService BookTemplateService { get; }

    public ContainersDataLoader ContainersDataLoader { get; }

    public ItemTemplateLoader ItemTemplateLoader { get; }

    public MobileTemplateLoader MobileTemplateLoader { get; }

    public LootTemplateLoader LootTemplateLoader { get; }

    public FactionTemplateLoader FactionTemplateLoader { get; }

    public SellProfileTemplateLoader SellProfileTemplateLoader { get; }

    public TemplateValidationLoader TemplateValidationLoader { get; }

    public TemplateValidatorRuntimeContext(
        DirectoriesConfig directoriesConfig,
        MoongateConfig config,
        ItemTemplateService itemTemplateService,
        MobileTemplateService mobileTemplateService,
        LootTemplateService lootTemplateService,
        FactionTemplateService factionTemplateService,
        SellProfileTemplateService sellProfileTemplateService,
        BookTemplateService bookTemplateService,
        ContainersDataLoader containersDataLoader,
        ItemTemplateLoader itemTemplateLoader,
        MobileTemplateLoader mobileTemplateLoader,
        LootTemplateLoader lootTemplateLoader,
        FactionTemplateLoader factionTemplateLoader,
        SellProfileTemplateLoader sellProfileTemplateLoader,
        TemplateValidationLoader templateValidationLoader
    )
    {
        DirectoriesConfig = directoriesConfig;
        Config = config;
        ItemTemplateService = itemTemplateService;
        MobileTemplateService = mobileTemplateService;
        LootTemplateService = lootTemplateService;
        FactionTemplateService = factionTemplateService;
        SellProfileTemplateService = sellProfileTemplateService;
        BookTemplateService = bookTemplateService;
        ContainersDataLoader = containersDataLoader;
        ItemTemplateLoader = itemTemplateLoader;
        MobileTemplateLoader = mobileTemplateLoader;
        LootTemplateLoader = lootTemplateLoader;
        FactionTemplateLoader = factionTemplateLoader;
        SellProfileTemplateLoader = sellProfileTemplateLoader;
        TemplateValidationLoader = templateValidationLoader;
    }
}
