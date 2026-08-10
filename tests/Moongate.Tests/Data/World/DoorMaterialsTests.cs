using Moongate.UO.Data.World;

namespace Moongate.Tests.Data.World;

/// <summary>
/// Which door belongs in a doorway, decided by the wall it is cut into.
/// <para>
/// Both reference implementations punt on this: ModernUO's generator hangs a dark wood door in every
/// doorway it finds, unconditionally, and moongatev2 ported that faithfully. But the client files
/// name every frame graphic by its material — <c>stone wall</c>, <c>wooden wall</c>, <c>marble
/// wall</c> — so the choice can be read off the map rather than invented.
/// </para>
/// </summary>
public class DoorMaterialsTests
{
    [Theory]
    [InlineData("wooden wall", "strong_wood_door")]
    [InlineData("log wall", "strong_wood_door")]
    [InlineData("stone wall", "metal_door")]
    [InlineData("brick wall", "metal_door")]
    [InlineData("sandstone wall", "metal_door")]
    [InlineData("marble wall", "dark_wood_door")]
    [InlineData("plaster wall", "dark_wood_door")]
    public void TheWallDecidesTheDoor(string wallName, string templateId)
        => Assert.Equal(templateId, DoorMaterials.TemplateFor(wallName));

    // What the references do for everything, and what we do for anything unrecognised.
    [Theory, InlineData("arrow loop"), InlineData("crystalline fragmen"), InlineData(""), InlineData(null)]
    public void AWallNobodyMapped_FallsBackToTheReferenceDoor(string? wallName)
        => Assert.Equal("dark_wood_door", DoorMaterials.TemplateFor(wallName));

    // The client files pad and case names inconsistently; the material is what matters.
    [Fact]
    public void TheMatchIgnoresCaseAndPadding()
        => Assert.Equal("metal_door", DoorMaterials.TemplateFor("  Stone Wall  "));

    /// <summary>
    /// Thirteen of the west frames are windows. A window is not a doorway, and the reference
    /// generators hang a door in every one of them — which nobody noticed while every generated door
    /// looked the same.
    /// </summary>
    [Theory, InlineData("window"), InlineData("Window")]
    public void AWindow_IsNotADoorway(string wallName)
        => Assert.False(DoorMaterials.IsDoorway(wallName));

    [Theory, InlineData("stone wall"), InlineData("wooden wall"), InlineData("arrow loop")]
    public void AWall_IsADoorway(string wallName)
        => Assert.True(DoorMaterials.IsDoorway(wallName));
}
