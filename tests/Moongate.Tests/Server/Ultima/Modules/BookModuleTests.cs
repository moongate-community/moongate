using Lua;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class BookModuleTests
{
    [Fact]
    public async Task GiveWriteOpen_WithLuaValues_WorkSynchronously()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
        {
            var module = new BookModule(fixture.Books, fixture.Items, fixture.World.Mobiles);
            var values = new LuaTable { ["contact_name"] = "Vega" };
            var serial = module.Give(2, "welcome_letter", values);
            Assert.Equal(0x40000F00L, serial);
            Assert.True(module.Write(serial!.Value, "welcome_letter", 3, values));
            Assert.True(module.Open(serial.Value, 2));
            Assert.Contains("Dear Bruno,<br><br>Bring this to Vega.", Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings);
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4294967296)]
    [InlineData(99)]
    public async Task UnknownOrOutOfRangeSerial_ReturnsDocumentedFailure(long serial)
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
        {
            var module = new BookModule(fixture.Books, fixture.Items, fixture.World.Mobiles);
            Assert.Null(module.Give(serial, "welcome_letter"));
            Assert.False(module.Write(serial, "welcome_letter", 2));
            Assert.False(module.Write(0x40000F00L, "welcome_letter", serial));
            Assert.False(module.Open(serial, 2));
        });
    }

    [Fact]
    public async Task Give_UnsupportedLuaValue_CreatesNothing()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
        {
            var module = new BookModule(fixture.Books, fixture.Items, fixture.World.Mobiles);
            Assert.Null(module.Give(2, "welcome_letter", new LuaTable { ["contact_name"] = new LuaTable() }));
            Assert.Null(module.Give(2, "welcome_letter", new LuaTable { [1] = "Vega" }));
            Assert.Single(fixture.Serials.Serials);
        });
    }
}
