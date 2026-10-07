using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Services.Internal.Books;
using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

[Collection(PostgresTestCollection.Name)]
public sealed class BookPersistenceTests
{
    [Fact]
    public async Task WorldSave_Reload_PreservesRenderedSnapshotDespiteNewSourceAndReader()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await using var host = await HostPersistenceFixture.CreateAsync(false);
        host.Container.RegisterInstance<IMobileService>(fixture.World.Mobiles);
        host.Container.RegisterInstance<IItemService>(fixture.Items);
        host.Container.AddLiveWorldMobiles().AddLiveWorldItems();
        await CoreMigrationFiles.ApplyAsync(host.Database, "world");
        await host.Owner.InitializeAsync();
        ItemEntity? created = null;
        await fixture.OnLoopAsync(() =>
            {
                fixture.Source.Attachments.Add(
                    new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100"), Hue = HueSpec.FromValue(42) }
                );
                created = fixture.Give();
                fixture.Items.PlaceOnGround(created, MapType.Trammel, new Point3D(1600, 1600, 0));
            }
        );
        // Capture on the real loop, then write through the actual persistence coordinator.
        await host.Owner.SaveAllAsync((capture, _) => fixture.OnLoopAsync(capture));
        var data = host.Container.Resolve<IDataAccess<ItemEntity>>();
        var loaded = await data.GetByIdAsync(created!.Id);
        Assert.NotNull(loaded);
        Assert.Equal(
            created.GetProp<string>(BookAttachmentCodec.PropKey),
            loaded.GetProp<string>(BookAttachmentCodec.PropKey)
        );
        Assert.True(BookAttachmentCodec.TryDecode(loaded.GetProp<string>(BookAttachmentCodec.PropKey), out var batch));
        Assert.Equal((100, (ushort)42), (Assert.Single(batch!.Items).Amount, batch.Items[0].Hue));
        Assert.DoesNotContain(await data.GetAllAsync(), item => item.TemplateId == "gold");
        Assert.Equal("Welcome Pippo", loaded.Name);
        Assert.Equal("welcome_letter", loaded.GetProp<string>("book.template"));
        Assert.Equal("British", loaded.GetProp<string>("book.author"));
        Assert.Equal("Welcome Pippo", loaded.GetProp<string>("book.title"));
        await fixture.OnLoopAsync(() =>
            {
                fixture.Items.Remove([created.Id]);
                fixture.Source.Attachments[0].Amount = DiceSpec.Parse("999");
                fixture.Source.Attachments[0].Hue = HueSpec.FromValue(99);
                fixture.Source.Content = "New source $player_name";
                fixture.Player.Name = "Aria";
                fixture.Items.Add([loaded]);
                Assert.True(fixture.Books.Open(loaded, fixture.Other));
                Assert.Contains(
                    "Dear Pippo,<br><br>Bring this to Vega.",
                    Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings
                );
            }
        );
    }
}
