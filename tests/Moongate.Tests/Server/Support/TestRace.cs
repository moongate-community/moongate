using Moongate.UO.Data.Races.Base;

namespace Moongate.Tests.Server.Support;

public sealed class TestRace : Race
{
    public TestRace(string name, int raceIndex)
        : base(raceID: raceIndex + 1, raceIndex, name, $"{name}s", 400, 401, 402, 403)
    {
    }

    public override int ClipHairHue(int hue) => hue;

    public override int ClipSkinHue(int hue) => hue;

    public override int RandomFacialHair(bool female) => 0;

    public override int RandomHair(bool female) => 0;

    public override int RandomHairHue() => 0;

    public override int RandomSkinHue() => 0;

    public override bool ValidateFacialHair(bool female, int itemID) => true;

    public override bool ValidateHair(bool female, int itemID) => true;
}
