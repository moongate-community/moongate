using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Ultima.Schedule;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class ScheduleModuleTests : IAsyncLifetime
{
    private ScheduleServices _services = null!;

    public async Task InitializeAsync()
    {
        _services = await ScheduleServices.CreateAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public void IsActive_TellsTheStateOfAnEvent_FalseForAnUnknownOne()
    {
        var result = Run(
            "return schedule.is_active('halloween'), schedule.is_active('winter'), schedule.is_active('nothing')"
        );

        Assert.Equal([true, false, false], result.Select(value => value.Read<bool>()));
    }

    [Fact]
    public void Active_ListsTheEventsThatAreOn()
    {
        var result = Run("local list = schedule.active() return #list, list[1].id, list[1].name, list[1].mode");

        Assert.Equal(1, result[0].Read<int>());
        Assert.Equal(["halloween", "Halloween", "auto"], result[1..].Select(value => value.Read<string>()));
    }

    [Fact]
    public void Events_ListsEveryEventWithItsFields()
    {
        var result = Run(
            "local e = schedule.events()[2] return #schedule.events(), e.id, e.name, e.from, e.to, e.mode, e.active"
        );

        Assert.Equal(2, result[0].Read<int>());
        Assert.Equal(["winter", "Winter", "12-20", "01-06", "auto"], result[1..6].Select(value => value.Read<string>()));
        Assert.False(result[6].Read<bool>());
    }

    [Fact]
    public void SetEvent_ChangesTheMode_AndTheHookRuns()
    {
        var result = Run("return schedule.set_event('halloween', 'off'), schedule.is_active('halloween')");

        Assert.Equal([true, false], result.Select(value => value.Read<bool>()));
        Assert.Equal("on_end", _services.Scripts.Calls[^1].Function);
    }

    [Theory, InlineData("'nothing', 'on'"), InlineData("'halloween', 'maybe'")]
    public void SetEvent_ABadIdOrMode_IsFalse(string arguments)
    {
        Assert.False(Run($"return schedule.set_event({arguments})")[0].Read<bool>());
    }

    [Fact]
    public void Next_IsTheUnixSecondsOfTheNextStop_NilForAnUnknownId()
    {
        // 2026-10-26 04:00Z.
        var result = Run("return schedule.next('nightly_restart'), schedule.next('nothing'), schedule.next('halloween')");

        Assert.Equal(new DateTimeOffset(2026, 10, 26, 4, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), result[0].Read<long>());
        Assert.Equal([LuaValue.Nil, LuaValue.Nil], result[1..]);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new ScheduleModule(_services.Events, _services.Schedule));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
