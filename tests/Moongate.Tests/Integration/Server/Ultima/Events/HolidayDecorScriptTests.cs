using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Integration.Server.Ultima.Events;

/// <summary>
///     The shipped
///     <c>
///         holiday_decor.lua
///     </c>
///     run against stand-ins for the modules it calls, written in Lua at the top
///     of each run: two maps, a few towns, and a world where some cells are taken or have no floor.
/// </summary>
public sealed class HolidayDecorScriptTests
{
    private const string Prelude = """
                                   MapType = { Felucca = 0, Trammel = 1 }
                                   log = { created = {}, deleted = {}, props = {}, timers = {} }
                                   limit = 1000
                                   timer = { after = function(seconds, callback) log.timers[#log.timers + 1] = callback end }
                                   taken = {}
                                   lying = {}
                                   nofloor = {}
                                   floor = 0
                                   serial = 100
                                   towns = { Britain = { name = 'Britain', category = 'Factions/Towns', x = 1000, y = 1000, z = 0 },
                                             Yew = { name = 'Yew', category = 'Factions/Towns', x = 500, y = 500, z = 0 } }
                                   locations = { find = function(text, map)
                                       if towns[text] then return { { name = 'Yew-Britain Brigand Camp', category = 'Camps', x = 1, y = 1, z = 0 }, towns[text] } end
                                       return {}
                                   end }
                                   world = {
                                       get_prop = function(k) return log.props[k] end,
                                       set_prop = function(k, v) log.props[k] = v return true end,
                                       standing_z = function(map, x, y, z) if nofloor[x .. ',' .. y] then return nil end return floor end,
                                       is_occupied = function(map, x, y) return taken[x .. ',' .. y] == true end,
                                   items_in_range = function(map, x, y, r) if lying[x .. ',' .. y] then return { 5 } end return {} end,
                                   }
                                   item = {
                                       create = function(t, map, x, y, z) if #log.created >= limit then return nil end serial = serial + 1 log.created[#log.created + 1] = { t, map, x, y, z, serial } return serial end,
                                       delete = function(s) log.deleted[#log.deleted + 1] = s return true end,
                                   }
                                   """;

    [Fact]
    public void Place_DecoratesEveryTownOfBothMaps_WithTheTemplatesInTurn()
    {
        // Two towns with a center on both maps, 8 spots each.
        var result = Run(
            "TOWNS_OK = true local n = m.place('x', { 'a', 'b' }) return n, #log.created, log.created[1][1], log.created[2][1], log.created[3][1]"
        );

        Assert.Equal(32, result[0].Read<int>());
        Assert.Equal(32, result[1].Read<int>());
        Assert.Equal(["a", "b", "a"], result[2..].Select(value => value.Read<string>()));
    }

    [Fact]
    public void Place_SkipsTheCellsTakenOrWithoutFloor_OrOnAnotherLevel()
    {
        var result = Run(
            "taken['1003,1003'] = true nofloor['997,1003'] = true floor = 0 "
            + "local n = m.place('x', { 'a' }) return n"
        );

        // Two cells of Britain are lost on each map.
        Assert.Equal(28, result[0].Read<int>());
    }

    [Fact]
    public void Place_SkipsACellWithAnItemOnIt()
    {
        Assert.Equal(30, Run("lying['1003,1003'] = true return m.place('x', { 'a' })")[0].Read<int>());
    }

    [Fact]
    public void Place_SkipsAFloorOnAnotherLevel()
    {
        Assert.Equal(0, Run("floor = 20 return m.place('x', { 'a' })")[0].Read<int>());
    }

    [Fact]
    public void Place_Twice_PlacesOnce()
    {
        var result = Run("m.place('x', { 'a' }) local again = m.place('x', { 'a' }) return again, #log.created");

        Assert.Equal(0, result[0].Read<int>());
        Assert.Equal(32, result[1].Read<int>());
    }

    [Fact]
    public void Remove_DeletesWhatWasPlaced_AndForgetsIt()
    {
        var result = Run(
            "local made = m.place('x', { 'a' }) local gone = m.remove('x') return made, gone, #log.deleted, log.props['holiday.x.items'], m.remove('x')"
        );

        Assert.Equal(32, result[0].Read<int>());
        Assert.Equal(32, result[1].Read<int>());
        Assert.Equal(32, result[2].Read<int>());
        Assert.Equal(LuaValue.Nil, result[3]);
        Assert.Equal(0, result[4].Read<int>());
    }

    [Fact]
    public void Remove_AfterARestart_StillFindsTheSerials()
    {
        // The prop is all that is kept: a fresh script state reads it back.
        var result = Run("log.props['holiday.x.items'] = '101,102,103' return m.remove('x'), #log.deleted");

        Assert.Equal(3, result[0].Read<int>());
        Assert.Equal(3, result[1].Read<int>());
    }

    [Fact]
    public void WhenTheServerRunsOutOfSerials_TheRestFollowsAfterAPause()
    {
        var result = Run(
            "limit = 10 local first = m.place('x', { 'a' }) local waiting = #log.timers limit = 1000 log.timers[1]() "
            + "return first, waiting, #log.created, #log.timers"
        );

        Assert.Equal(10, result[0].Read<int>());
        Assert.Equal(1, result[1].Read<int>());
        Assert.Equal(32, result[2].Read<int>());
        Assert.Equal(1, result[3].Read<int>());
    }

    [Fact]
    public void Remove_BeforeThePauseEnds_StopsTheWork()
    {
        var result = Run(
            "limit = 10 m.place('x', { 'a' }) local gone = m.remove('x') limit = 1000 log.timers[1]() "
            + "return gone, #log.created, log.props['holiday.x.items']"
        );

        Assert.Equal(10, result[0].Read<int>());
        Assert.Equal(10, result[1].Read<int>());
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    [Fact]
    public void ARemoveAndANewPlaceDuringAPause_LeaveTheOldTimerHarmless()
    {
        var result = Run(
            "limit = 10 m.place('x', { 'a' }) m.remove('x') limit = 1000 m.place('x', { 'a' }) local old = log.timers[1] old() "
            + "return #log.created, #log.deleted"
        );

        // 10 made, 10 removed, then the whole set again; the old timer added nothing.
        Assert.Equal(42, result[0].Read<int>());
        Assert.Equal(10, result[1].Read<int>());
    }

    [Fact]
    public void ASpotThatNeverGetsASerial_IsGivenUpAfterSomeTries()
    {
        var result = Run(
            "limit = 0 m.place('x', { 'a' }) local pauses = 0 "
            + "while log.timers[1] and pauses < 200 do local t = table.remove(log.timers, 1) pauses = pauses + 1 t() end "
            + "return pauses, #log.created");

        Assert.True(result[0].Read<int>() < 200);
        Assert.Equal(0, result[1].Read<int>());
    }

    [Theory,
     InlineData("Britain"), InlineData("Trinsic"), InlineData("Vesper"), InlineData("Minoc"), InlineData("Yew"),
     InlineData("Skara Brae"), InlineData("Moonglow")]
    public void TheTownsTheScriptDecorates_AreInTheShippedLocationsOfFelucca(string town)
    {
        var text = File.ReadAllText(ShippedScript("../data/locations.toml"));

        Assert.Contains($"map = \"felucca\"\ncategory = \"Factions/Towns\"\nname = \"{town}\"", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ANoTemplates_PlacesNothing()
    {
        Assert.Equal(0, Run("return m.place('x', {})")[0].Read<int>());
    }

    private static LuaValue[] Run(string body)
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var script = File.ReadAllText(ShippedScript("common/holiday_decor.lua"));
        var chunk = $"{Prelude}\nm = (function()\n{script}\nend)()\n{body}";

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
