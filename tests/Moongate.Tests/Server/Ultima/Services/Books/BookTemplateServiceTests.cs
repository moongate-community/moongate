using System.Globalization;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookTemplateServiceTests
{
    private static readonly TextTemplateContext Context = new() { PlayerName = "Pippo", ServerName = "Moongate", UsersOnline = 3 };

    [Fact]
    public void TryRender_AWritableSource_IsRenderedBlank_WithItsPages()
    {
        var service = new BookTemplateService(
            new StubDataLoaderService().With(
                new BookTemplate { Id = "blank", Title = "a book", Author = "$player_name", Content = "", Writable = true, Pages = 30 },
                new BookTemplate { Id = "default", Title = "a book", Content = "", Writable = true }
            )
        );

        Assert.True(service.TryRender("blank", Context, "eng", null, out var blank));
        Assert.True(service.TryRender("default", Context, "eng", null, out var byDefault));

        Assert.Equal(("a book", "Pippo", "", true, 30), (blank!.Title, blank.Author, blank.Content, blank.Writable, blank.Pages));
        Assert.Equal((true, 20), (byDefault!.Writable, byDefault.Pages));
    }

    [Fact]
    public void TryRender_CarriesTheItemIdOfTheSource_WhateverTheLanguage()
    {
        var service = Service(new BookTemplate
        {
            Id = "tome", Title = "Tome", Content = "Text", ItemId = 0x0FF2,
            Translations = new() { ["ita"] = new() { Title = "Tomo" } }
        });

        Assert.True(service.TryRender("tome", Context, "ita", null, out var rendered));

        Assert.Equal(("Tomo", 0x0FF2), (rendered!.Title, rendered.ItemId));
    }

    [Fact]
    public void TryRender_TranslationAndLiteralCustomValues_UsesCreationContext()
    {
        var service = Service(new BookTemplate
        {
            Id = "note", Title = "Welcome $player_name", Author = "British", Content = "Hello",
            Variables = ["contact_name", "fine", "paid"],
            Translations = new() { ["ita"] = new() { Content = "Caro ${player_name}, $server_name: $contact_name $fine $paid" } }
        });
        using var culture = new Moongate.Tests.TestSupport.Strings.CultureScope("it-IT");
        Assert.True(service.TryRender("note", Context, "ita", new Dictionary<string, object?>
        {
            ["contact_name"] = "$server_name", ["fine"] = 1.5, ["paid"] = true
        }, out var rendered));
        Assert.NotNull(rendered);
        Assert.Equal("Welcome Pippo", rendered.Title);
        Assert.Equal("British", rendered.Author);
        Assert.Equal("Caro Pippo, Moongate: $server_name 1.5 true", rendered.Content);
        Assert.Equal("readable_scroll", rendered.ItemTemplateId);
        Assert.False(service.TryGet("NOTE", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Vega")]
    public void TryRender_SuppliedString_IsAccepted(string value)
    {
        Assert.True(Service().TryRender("note", Context, "eng", new Dictionary<string, object?> { ["value"] = value }, out var book));
        Assert.Equal(value, book?.Content);
    }

    [Fact]
    public void TryRender_MissingExtraAndUnsupportedValues_AreRefused()
    {
        var service = Service();
        Assert.False(service.TryRender("note", Context, "eng", null, out _));
        Assert.False(service.TryRender("missing", Context, "eng", null, out _));
        Assert.False(service.TryRender("note", Context, "eng", new Dictionary<string, object?> { ["value"] = "ok", ["extra"] = 1 }, out _));
        foreach (var value in new object?[] { null, new Dictionary<string, string>(), double.NaN, double.PositiveInfinity, float.NegativeInfinity, "secret\0text", "secret\u0001text" })
        {
            Assert.False(service.TryRender("note", Context, "eng", new Dictionary<string, object?> { ["value"] = value }, out var book));
            Assert.Null(book);
        }
    }

    [Theory]
    [InlineData("title", 128, true)]
    [InlineData("title", 129, false)]
    [InlineData("author", 128, true)]
    [InlineData("author", 129, false)]
    [InlineData("content", 16384, true)]
    [InlineData("content", 16385, false)]
    public void TryRender_ExpandedFieldBounds_AreChecked(string field, int length, bool expected)
    {
        var template = new BookTemplate { Id = "note", Title = "Note", Content = "Text", Variables = ["value"] };
        if (field == "title") template.Title = "$value";
        if (field == "author") template.Author = "$value";
        if (field == "content") template.Content = "$value";
        Assert.Equal(expected, Service(template).TryRender("note", Context, "eng",
            new Dictionary<string, object?> { ["value"] = new string('x', length) }, out _));
    }

    [Fact]
    public void TryRender_UnusedDeclaredValueWithControls_IsRefused()
    {
        var template = new BookTemplate { Id = "note", Title = "Note", Content = "Text", Variables = ["value"] };
        Assert.False(Service(template).TryRender("note", Context, "eng",
            new Dictionary<string, object?> { ["value"] = "secret\0text" }, out _));
    }

    private static BookTemplateService Service(BookTemplate? template = null)
    {
        return new(new StubDataLoaderService().With(template ?? new BookTemplate
        {
            Id = "note", Title = "Note", Content = "$value", Variables = ["value"]
        }));
    }
}
