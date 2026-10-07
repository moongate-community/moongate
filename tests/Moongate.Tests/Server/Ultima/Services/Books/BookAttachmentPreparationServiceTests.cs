using System.Collections.Immutable;
using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookAttachmentPreparationServiceTests
{
    [Fact]
    public void Prepare_StackAndNonstackEntries_FreezesFourSerialFreeItems()
    {
        var tiles = new FakeTileDataService().Item(0xEED, TileFlagType.Generic, 0);
        var gold = new ItemTemplate
        {
            Id = "gold", ItemId = new(0xEED), Hue = HueSpec.FromValue(42),
            Rarity = EnumValueSpec<ItemRarityType>.FromValue(ItemRarityType.Legendary)
        };
        var bread = new ItemTemplate { Id = "bread", ItemId = new(0x103B), Stackable = false };
        var templates = new ItemTemplateService(new StubDataLoaderService().With(gold, bread));
        var factory = new FakeItemFactoryService(templates, tiles);
        var service = new BookAttachmentPreparationService(factory, templates, tiles);
        var source = new BookTemplateSource
        {
            Attachments =
            [
                new() { ItemTemplate = "gold", Amount = DiceSpec.FromValue(100), Newbie = true },
                new() { ItemTemplate = "bread", Amount = DiceSpec.FromValue(3), Hue = HueSpec.FromValue(123) }
            ]
        };
        var encoded = service.Prepare(source);
        Assert.True(BookAttachmentCodec.TryDecode(encoded, out var batch));
        Assert.NotNull(batch);
        Assert.Equal(4, batch.Items.Length);
        Assert.Equal(100, batch.Items[0].Amount);
        Assert.Equal((ushort)42, batch.Items[0].Hue);
        Assert.Equal(ItemRarityType.Legendary, batch.Items[0].Rarity);
        Assert.All(batch.Items.Skip(1), item => Assert.Equal(1, item.Amount));
        Assert.All(batch.Items.Skip(1), item => Assert.Equal((ushort)123, item.Hue));
        gold.Hue = HueSpec.FromValue(900);
        source.Attachments[0].Amount = DiceSpec.FromValue(1);
        var materialized = BookAttachmentCodec.Materialize(batch.Items[0]);
        Assert.Equal(100, materialized.Amount);
        Assert.Equal(new Hue(42), materialized.Hue);
        Assert.Equal(LootType.Newbied, materialized.GetProp<LootType>(ItemPropKeys.LootType));
        Assert.Equal(Serial.Zero, materialized.Id);
        Assert.Null(materialized.ContainerId);
        Assert.Empty(factory.Saved);
    }

    [Fact]
    public void Prepare_EmptySource_HasNoPayload()
    {
        var tiles = new FakeTileDataService();
        var templates = new ItemTemplateService(new StubDataLoaderService().With(Array.Empty<ItemTemplate>()));
        Assert.Null(
            new BookAttachmentPreparationService(new FakeItemFactoryService(templates, tiles), templates, tiles)
                .Prepare(new())
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    [InlineData(65536)]
    public void Prepare_InvalidBatch_RejectsBeforeReturningEntitlement(int count)
    {
        var tiles = new FakeTileDataService();
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "bread", ItemId = new(0x103B), Stackable = false }
            )
        );
        var service = new BookAttachmentPreparationService(new FakeItemFactoryService(templates, tiles), templates, tiles);
        Assert.Throws<InvalidDataException>(() => service.Prepare(
                new()
                {
                    Attachments = [new() { ItemTemplate = "bread", Amount = DiceSpec.FromValue(count) }]
                }
            )
        );
    }

    [Fact]
    public void EncodeDecode_ValidScalarProps_RoundTripsWithoutSharedState()
    {
        var props = ImmutableDictionary<string, JsonElement>.Empty
            .Add("text", JsonSerializer.SerializeToElement("hello"))
            .Add("flag", JsonSerializer.SerializeToElement(true))
            .Add("quality", JsonSerializer.SerializeToElement(1.25m));
        var payload = new BookAttachmentPayload
        {
            Items =
            [
                new()
                {
                    TemplateId = "bread", ItemId = 0x103B, Amount = 1, Props = props
                }
            ]
        };
        var text = BookAttachmentCodec.Encode(payload);
        Assert.True(BookAttachmentCodec.TryDecode(text, out var decoded));
        Assert.NotNull(decoded);
        var first = BookAttachmentCodec.Materialize(decoded.Items[0]);
        var second = BookAttachmentCodec.Materialize(decoded.Items[0]);
        first.SetProp("text", "changed");
        Assert.Equal("hello", second.GetProp<string>("text"));
        Assert.True(second.GetProp<bool>("flag"));
        Assert.Equal(1.25m, second.GetProp<decimal>("quality"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"Version\":2,\"Items\":[]}")]
    [InlineData("{\"Version\":1,\"Items\":[]}")]
    [InlineData("{\"Version\":1,\"Items\":[{\"TemplateId\":\"x\",\"Amount\":0}]}")]
    [InlineData("{\"Version\":1,\"Items\":[{\"TemplateId\":\"x\",\"ItemId\":5,\"Amount\":1,\"Props\":{\"a\":[]}}]}")]
    [InlineData("{\"Version\":1,\"Items\":[{\"TemplateId\":\"x\",\"ItemId\":5,\"Amount\":1,\"Props\":{\"a\":1,\"a\":2}}]}")]
    public void Decode_MalformedOrUnsupportedInput_RefusesClaim(string? text)
    {
        Assert.False(BookAttachmentCodec.TryDecode(text, out var payload));
        Assert.Null(payload);
    }

    [Fact]
    public void Decode_TooManyItemsOrOversizedPayload_RefusesClaim()
    {
        var item = "{\"TemplateId\":\"x\",\"ItemId\":5,\"Amount\":1}";
        Assert.False(
            BookAttachmentCodec.TryDecode(
                "{\"Version\":1,\"Items\":[" +
                string.Join(',', Enumerable.Repeat(item, 33)) + "]}",
                out _
            )
        );
        Assert.False(BookAttachmentCodec.TryDecode(new string(' ', 65537), out _));
    }
}
