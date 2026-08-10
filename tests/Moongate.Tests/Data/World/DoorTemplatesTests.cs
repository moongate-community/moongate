using Moongate.UO.Data.World;

namespace Moongate.Tests.Data.World;

/// <summary>
/// The corpus declares 13 door classes across 944 objects. Each has a template that already carries
/// <c>ScriptId: items.door</c> — this is the map between the two, and the reason a placed door is a
/// working door with no further wiring.
/// </summary>
public class DoorTemplatesTests
{
    [Theory]
    [InlineData("MetalDoor", "metal_door")]
    [InlineData("MetalDoor2", "metal_door_2")]
    [InlineData("BarredMetalDoor", "barred_metal_door")]
    [InlineData("BarredMetalDoor2", "barred_metal_door_2")]
    [InlineData("StrongWoodDoor", "strong_wood_door")]
    [InlineData("DarkWoodDoor", "dark_wood_door")]
    [InlineData("LightWoodDoor", "light_wood_door")]
    [InlineData("RattanDoor", "rattan_door")]
    [InlineData("SecretDungeonDoor", "secret_dungeon_door")]
    [InlineData("SecretWoodenDoor", "secret_wooden_door")]
    [InlineData("SecretStoneDoor1", "secret_stone_door_1")]
    [InlineData("SecretStoneDoor2", "secret_stone_door_2")]
    [InlineData("SecretStoneDoor3", "secret_stone_door_3")]
    public void EveryDeclaredDoorClass_HasATemplate(string declaredType, string templateId)
        => Assert.Equal(templateId, DoorTemplates.For(declaredType));

    [Theory, InlineData("Static"), InlineData("LibraryBookcase"), InlineData("Barrel"), InlineData("")]
    public void AnythingThatIsNotADoor_HasNone(string declaredType)
        => Assert.Null(DoorTemplates.For(declaredType));

    // The corpus is upstream data: a class nobody has looked at must place as scenery rather than be
    // guessed into a door by its name.
    [Fact]
    public void AnUnknownDoorLikeName_IsNotGuessed()
        => Assert.Null(DoorTemplates.For("PlasmaDoor"));

    // The declared types come from RunUO class names, which are case-sensitive.
    [Fact]
    public void TheMatchIsExact()
        => Assert.Null(DoorTemplates.For("metaldoor"));
}
