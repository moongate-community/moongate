using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookContextFactoryTests
{
    [Fact]
    public async Task Capture_Builtins_UsesRecipientAndConnectedCharacterCount()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
        {
            var context = fixture.Contexts.Capture(fixture.Player);
            Assert.Equal("Pippo", context.PlayerName);
            Assert.Equal("Moongate", context.ServerName);
            Assert.Equal("Felucca", context.RealmName);
            Assert.Equal(2, context.UsersOnline);
            Assert.NotEmpty(context.Version);
            Assert.NotEmpty(context.Codename);
            Assert.Equal("Recorded", fixture.Contexts.Capture(fixture.Player, "Recorded").PlayerName);
        });
    }
    [Fact]
    public async Task CaptureForCreation_UsesAnUnloadedCharacterNameOffTheGameLoop()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        var context = fixture.Contexts.CaptureForCreation("New Player");
        Assert.Equal("New Player", context.PlayerName);
        Assert.Equal("Moongate", context.ServerName);
        Assert.Equal("Felucca", context.RealmName);
        Assert.Equal(2, context.UsersOnline);
        Assert.NotEmpty(context.Version);
        Assert.NotEmpty(context.Codename);
    }

}
