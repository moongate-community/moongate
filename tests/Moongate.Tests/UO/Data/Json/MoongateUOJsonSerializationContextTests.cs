using Moongate.Core.Json;
using Moongate.UO.Data.Expansions;
using Moongate.UO.Data.Json;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Json.Locations;
using Moongate.UO.Data.Json.Names;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Json.Weather;
using Moongate.UO.Data.Skills;
using System.Text.Json;

namespace Moongate.Tests.UO.Data.Json;

public class MoongateUOJsonSerializationContextTests
{
    [Test]
    public void Context_ShouldRegister_AllJsonRootTypesUsedByLoaders()
    {
        var context = MoongateUOJsonSerializationContext.Default;

        Assert.Multiple(
            () =>
            {
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(SkillInfo[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(ExpansionInfo[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonContainerSize[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonNameDef[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonMapLocations)), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonRegion[])), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonWeatherWrap)), Is.True);
                Assert.That(JsonContextTypeResolver.IsTypeRegistered(context, typeof(JsonProfessionsRoot)), Is.True);
            }
        );
    }

    [Test]
    public void Deserialize_AssetJsonFiles_ShouldSucceedForAllLoaderTargets()
    {
        var dataRoot = GetAssetsDataRoot();
        var containersFile = Path.Combine(dataRoot, "containers", "default_containers.json");
        var namesFile = Path.Combine(dataRoot, "names", "modernuo_names.json");
        var locationsFile = Path.Combine(dataRoot, "locations", "felucca.json");
        var regionsFile = Path.Combine(dataRoot, "regions", "regions.json");
        var weatherFile = Path.Combine(dataRoot, "weather", "weather.json");
        var expansionsFile = Path.Combine(dataRoot, "expansions.json");
        var skillsFile = Path.Combine(dataRoot, "skills.json");
        var professionsFile = Path.Combine(dataRoot, "Professions", "professions.json");

        var context = MoongateUOJsonSerializationContext.Default;
        var containers = JsonUtils.DeserializeFromFile<JsonContainerSize[]>(containersFile, context);
        var names = JsonUtils.DeserializeFromFile<JsonNameDef[]>(namesFile, context);
        var locations = JsonUtils.DeserializeFromFile<JsonMapLocations>(locationsFile, context);
        var regions = JsonUtils.DeserializeFromFile<JsonRegion[]>(regionsFile, context);
        var weather = JsonUtils.DeserializeFromFile<JsonWeatherWrap>(weatherFile, context);
        var expansions = JsonUtils.DeserializeFromFile<ExpansionInfo[]>(expansionsFile, context);
        var skills = JsonUtils.DeserializeFromFile<SkillInfo[]>(skillsFile, context);
        var professions = JsonUtils.DeserializeFromFile<JsonProfessionsRoot>(professionsFile, context);

        Assert.Multiple(
            () =>
            {
                Assert.That(containers, Is.Not.Null);
                Assert.That(containers.Length, Is.GreaterThan(0));
                Assert.That(containers.All(static container => !string.IsNullOrWhiteSpace(container.Id)), Is.True);
                Assert.That(names, Is.Not.Null);
                Assert.That(names.Length, Is.GreaterThan(0));
                Assert.That(locations, Is.Not.Null);
                Assert.That(locations.Categories.Count, Is.GreaterThan(0));
                Assert.That(regions, Is.Not.Null);
                Assert.That(regions.Length, Is.GreaterThan(0));
                Assert.That(weather, Is.Not.Null);
                Assert.That(weather.WeatherTypes.Count, Is.GreaterThan(0));
                Assert.That(expansions, Is.Not.Null);
                Assert.That(expansions.Length, Is.GreaterThan(0));
                Assert.That(skills, Is.Not.Null);
                Assert.That(skills.Length, Is.GreaterThan(0));
                Assert.That(professions, Is.Not.Null);
                Assert.That(professions.Professions.Length, Is.GreaterThan(0));
            }
        );
    }

    [Test]
    public void Deserialize_Regions_ShouldMaterializePolymorphicRegionTypes()
    {
        const string json = """
                            [
                              { "$type": "TownRegion", "Map": "Felucca", "Name": "Town", "Area": [ { "x1": 1, "y1": 1, "x2": 2, "y2": 2 } ] },
                              { "$type": "DungeonRegion", "Map": "Felucca", "Name": "Dungeon", "Area": [ { "x1": 3, "y1": 3, "x2": 4, "y2": 4 } ] },
                              { "$type": "GuardedRegion", "Map": "Felucca", "Name": "Guarded", "Area": [ { "x1": 5, "y1": 5, "x2": 6, "y2": 6 } ] },
                              { "$type": "NoHousingRegion", "Map": "Felucca", "Name": "NoHousing", "Area": [ { "x1": 7, "y1": 7, "x2": 8, "y2": 8 } ] },
                              { "$type": "GreenAcresRegion", "Map": "Felucca", "Name": "GreenAcres", "Area": [ { "x1": 9, "y1": 9, "x2": 10, "y2": 10 } ] },
                              { "$type": "JailRegion", "Map": "Felucca", "Name": "Jail", "Area": [ { "x1": 11, "y1": 11, "x2": 12, "y2": 12 } ] }
                            ]
                            """;

        var regions = JsonSerializer.Deserialize(
            json,
            MoongateUOJsonSerializationContext.Default.JsonRegionArray
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(regions, Has.Length.EqualTo(6));
                Assert.That(regions[0], Is.TypeOf<JsonTownRegion>());
                Assert.That(regions[1], Is.TypeOf<JsonDungeonRegion>());
                Assert.That(regions[2], Is.TypeOf<JsonGuardedRegion>());
                Assert.That(regions[3], Is.TypeOf<JsonNoHousingRegion>());
                Assert.That(regions[4], Is.TypeOf<JsonGreenAcresRegion>());
                Assert.That(regions[5], Is.TypeOf<JsonJailRegion>());
                Assert.That(regions[0].MapId, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void Deserialize_RegionParent_ShouldResolveParentMapId()
    {
        const string json = """
                            [
                              {
                                "$type": "TownRegion",
                                "Map": "Trammel",
                                "Name": "Child",
                                "Parent": { "Name": "Britain", "Map": "Felucca" },
                                "Area": [ { "x1": 1, "y1": 1, "x2": 2, "y2": 2 } ]
                              }
                            ]
                            """;

        var regions = JsonSerializer.Deserialize(
            json,
            MoongateUOJsonSerializationContext.Default.JsonRegionArray
        );
        var town = regions![0] as JsonTownRegion;

        Assert.Multiple(
            () =>
            {
                Assert.That(town, Is.Not.Null);
                Assert.That(town!.MapId, Is.EqualTo(1));
                Assert.That(town.Parent, Is.Not.Null);
                Assert.That(town.Parent!.MapId, Is.EqualTo(0));
            }
        );
    }

    [OneTimeSetUp]
    public void Setup()
        => JsonUtils.RegisterJsonContext(MoongateUOJsonSerializationContext.Default);

    private static string GetAssetsDataRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Moongate.Server", "Assets", "data");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate src/Moongate.Server/Assets/data from test base directory.");
    }
}
