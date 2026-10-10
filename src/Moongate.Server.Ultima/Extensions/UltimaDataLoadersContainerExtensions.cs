using DryIoc;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Containers;
using Moongate.Server.Ultima.Data.Crafts;
using Moongate.Server.Ultima.Data.Harvest;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Data.Schedule;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Data.Moongates;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Pets;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Data.Weather;
using Moongate.Server.Ultima.Loaders;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the loaders of data/ and templates/, in the order they load.
/// </summary>
public static class UltimaDataLoadersContainerExtensions
{
    /// <summary>
    ///     Registers the loaders of data/ and templates/, in the order they load.
    /// </summary>
    public static Container AddUltimaDataLoaders(this Container container)
    {
        container.AddUltimaDataLoader<MapLoader, MapContent>(0);
        container.AddUltimaDataLoader<StartingCitiesLoader, StartingCityContent>(1);
        container.AddUltimaDataLoader<SkillsLoader, SkillContent>(2);
        container.AddUltimaDataLoader<ProfessionsLoader, ProfessionContent>(3);
        container.AddUltimaDataLoader<RacesLoader, RaceContent>(4);
        container.AddUltimaDataLoader<BannedNamesLoader, BannedNamesContent>(5);
        container.AddUltimaDataLoader<ContainersLoader, ContainerContent>(6);
        container.AddUltimaDataLoader<BodiesLoader, BodyContent>(7);
        container.AddUltimaDataLoader<WeatherLoader, WeatherContent>(8);
        container.AddUltimaDataLoader<RegionsLoader, RegionContent>(9);
        container.AddUltimaDataLoader<MessagesLoader, MessageContent>(10);
        container.AddUltimaDataLoader<NamesLoader, NameList>(11);
        container.AddUltimaDataLoader<ItemTemplatesLoader, ItemTemplate>(12);
        container.AddUltimaDataLoader<LootTemplatesLoader, LootTemplate>(14);
        container.AddUltimaDataLoader<MobileTemplatesLoader, MobileTemplate>(15);
        container.AddUltimaDataLoader<MotdLoader, MotdLine>(16);
        container.AddUltimaDataLoader<TitlesLoader, FameKarmaTitle>(17);
        // After the mobile templates, which the lists and the spawns name.
        container.AddUltimaDataLoader<NpcListsLoader, NpcListTemplate>(18);
        container.AddUltimaDataLoader<SpawnsLoader, SpawnTemplate>(19);
        container.AddUltimaDataLoader<GumpsLoader, GumpTemplate>(20);
        container.AddUltimaDataLoader<MoongatesLoader, MoongateFacet>(21);
        container.AddUltimaDataLoader<LocationsLoader, NamedLocation>(22);
        container.AddUltimaDataLoader<JailLoader, JailFile>(23);
        container.AddUltimaDataLoader<BooksLoader, BookTemplate>(24);
        // Starting items may reference already validated book templates.
        container.AddUltimaDataLoader<StartingItemsLoader, StartingItemSet>(25);
        // Shops name item and mobile templates, which load before.
        container.AddUltimaDataLoader<ShopsLoader, ShopDefinition>(26);
        container.AddUltimaDataLoader<HarvestLoader, HarvestResource>(27);
        container.AddUltimaDataLoader<ScheduleLoader, ScheduleFile>(28);
        // Crafts name item templates, and the resource lists load before the crafts that name them.
        container.AddUltimaDataLoader<CraftResourcesLoader, CraftResourceList>(29);
        container.AddUltimaDataLoader<CraftsLoader, CraftDefinition>(30);
        // After the mobile templates, which the creatures name.
        container.AddUltimaDataLoader<TamingLoader, TamingCreature>(31);
        // After the item templates, which the kinds of food name.
        container.AddUltimaDataLoader<PetFoodLoader, PetFood>(32);
        // After the item templates, which the reagents and the scrolls of the spells name.
        container.AddUltimaDataLoader<SpellsLoader, SpellDefinition>(33);

        return container;
    }
}
