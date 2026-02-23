using System.Text.Json;
using Moongate.Server.Data.Entities;
using Moongate.Server.Services.Entities;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class PlaceholderResolverServiceTests
{
    [Test]
    public void Resolve_ShouldLeaveUnknownTokensUntouched()
    {
        var resolver = new PlaceholderResolverService();
        var args = JsonSerializer.SerializeToElement(
            new
            {
                value = "Hello <unknownToken>"
            }
        );

        var context = new StarterProfileContext(
            new()
            {
                ID = 1,
                Name = "Warrior"
            },
            null,
            GenderType.Male
        );

        var resolved = resolver.Resolve(args, context, "Bob");

        Assert.That(resolved.GetProperty("value").GetString(), Is.EqualTo("Hello <unknownToken>"));
    }

    [Test]
    public void Resolve_ShouldReplaceKnownTokensAndPreservePrimitiveTypes()
    {
        var resolver = new PlaceholderResolverService();
        var args = JsonSerializer.SerializeToElement(
            new
            {
                title = "<professionName> Handbook",
                author = "<playerName>",
                meta = new
                {
                    audience = "<raceName>-<gender>"
                },
                pages = 20,
                writable = true
            }
        );

        var context = new StarterProfileContext(
            new()
            {
                ID = 2,
                Name = "Mage"
            },
            new TestRace("elf", 1),
            GenderType.Female
        );

        var resolved = resolver.Resolve(args, context, "Iris");

        Assert.Multiple(
            () =>
            {
                Assert.That(resolved.GetProperty("title").GetString(), Is.EqualTo("Mage Handbook"));
                Assert.That(resolved.GetProperty("author").GetString(), Is.EqualTo("Iris"));
                Assert.That(resolved.GetProperty("meta").GetProperty("audience").GetString(), Is.EqualTo("elf-female"));
                Assert.That(resolved.GetProperty("pages").GetInt32(), Is.EqualTo(20));
                Assert.That(resolved.GetProperty("writable").GetBoolean(), Is.True);
            }
        );
    }
}
