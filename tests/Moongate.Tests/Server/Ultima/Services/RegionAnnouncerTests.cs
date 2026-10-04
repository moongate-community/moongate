using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Regions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class RegionAnnouncerTests
{
    private static readonly RegionContent Britain = Region("Britain", true);
    private static readonly RegionContent Field = Region("A Wheatfield in Britain 1", true, "Britain");
    private static readonly RegionContent Covetous = Region("Covetous", false);
    private static readonly RegionContent Trinsic = Region("Trinsic", true);
    private static readonly RegionContent OtherBritain = Region("Britain", true, map: MapType.Felucca);
    private static readonly RegionContent Nameless = Region(null, true);

    private readonly RecordingSpeechService _speech = new();
    private readonly MobileEntity _aria = new() { Id = new Serial(2), AccountId = new Serial(0x42), Name = "Aria" };
    private readonly RegionAnnouncer _announcer;

    public RegionAnnouncerTests()
    {
        _announcer = new(
            new StubDataLoaderService().With(Britain, Field, Covetous, Trinsic, OtherBritain, Nameless),
            _speech
        );
    }

    [Fact]
    public void IntoATown_ThePlayerReadsItsName_AndThatItsGuardsProtectIt()
    {
        _announcer.RegionChanged(_aria, null, Britain);

        Assert.Equal(
            ["You have entered Britain.", "You are now under the protection of the guards of Britain."],
            Told()
        );
    }

    [Fact]
    public void OutOfATown_ThePlayerReadsThatItLeftItsGuards_AndTheTown()
    {
        _announcer.RegionChanged(_aria, Britain, null);

        Assert.Equal(["You have left the protection of the guards of Britain.", "You have left Britain."], Told());
    }

    [Fact]
    public void InsideATown_FromOnePartToAnother_NothingIsSaid()
    {
        _announcer.RegionChanged(_aria, Britain, Field);
        _announcer.RegionChanged(_aria, Field, Britain);

        Assert.Empty(Told());
    }

    [Fact]
    public void FromAPartOfATown_ToTheWilderness_TheTownIsNamed_NotThePart()
    {
        _announcer.RegionChanged(_aria, Field, null);

        Assert.Equal(["You have left the protection of the guards of Britain.", "You have left Britain."], Told());
    }

    [Fact]
    public void FromATownToAPlaceWithNoGuards_BothAreNamed()
    {
        _announcer.RegionChanged(_aria, Britain, Covetous);

        Assert.Equal(
            [
                "You have left the protection of the guards of Britain.", "You have left Britain.",
                "You have entered Covetous."
            ],
            Told()
        );
    }

    [Fact]
    public void FromOneTownToAnother_TheGuardsChangeToo()
    {
        _announcer.RegionChanged(_aria, Britain, Trinsic);

        Assert.Equal(
            [
                "You have left the protection of the guards of Britain.", "You have left Britain.",
                "You have entered Trinsic.", "You are now under the protection of the guards of Trinsic."
            ],
            Told()
        );
    }

    [Fact]
    public void TheSameTownOnAnotherMap_IsTheSameName_SoNothingIsSaid()
    {
        _announcer.RegionChanged(_aria, Britain, OtherBritain);

        Assert.Empty(Told());
    }

    [Fact]
    public void AGuardedPlaceWithNoName_UsesTheClientsOwnTexts()
    {
        _announcer.RegionChanged(_aria, null, Nameless);
        _announcer.RegionChanged(_aria, Nameless, null);

        Assert.Empty(Told());
        Assert.Equal([500112, 500113], _speech.ToldClilocs.Select(told => told.Cliloc));
    }

    private string[] Told()
    {
        return _speech.Told.Select(told => told.Text).ToArray();
    }

    private static RegionContent Region(string? name, bool guarded, string? parent = null, MapType map = MapType.Trammel)
    {
        return new() { Map = map, Name = name, Guarded = guarded, Parent = parent };
    }
}
