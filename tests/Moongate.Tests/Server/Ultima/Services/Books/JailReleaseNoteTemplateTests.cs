using System.Globalization;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class JailReleaseNoteTemplateTests
{
    [Theory]
    [InlineData("eng")]
    [InlineData("ita")]
    [InlineData("fre")]
    [InlineData("ger")]
    [InlineData("spa")]
    [InlineData("por")]
    [InlineData("pol")]
    [InlineData("cze")]
    public async Task ShippedJailDocument_AllLanguages_KeepExistingWording(string language)
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        var directories = new DirectoriesConfig(Path.Combine(BookLuaFixture.RepositoryRoot(), "moongate_root"), ["data", "templates"]);
        var items = new StubDataLoaderService().With(fixture.ItemTemplates.Get("readable_scroll"), fixture.ItemTemplates.Get("jail_release_note"));
        var books = (await new BooksLoader(directories, items).LoadDataAsync()).Entities.ToArray();
        var service = new BookTemplateService(new StubDataLoaderService().With(books));
        var messages = (await new MessagesLoader(directories, new LocalizationConfig { Language = language }).LoadDataAsync()).Entities.ToDictionary(message => message.Id, message => message.Text);
        foreach (var reason in new[] { "", "Stole a horse" })
        {
            var reasonLine = reason == "" ? "" : " " + string.Format(CultureInfo.InvariantCulture, messages[30149], reason);
            Assert.True(service.TryRender("jail_release_note", new TextTemplateContext { PlayerName = "Recorded" }, language,
                new Dictionary<string, object?>
                {
                    ["days"] = 3, ["cell"] = 2, ["jailed_at"] = "2026-10-04", ["released_at"] = "2026-10-07",
                    ["fine"] = 500, ["jailed_by"] = "Giachi", ["reason_line"] = reasonLine
                }, out var note));
            Assert.Equal(string.Format(CultureInfo.InvariantCulture, messages[30143], "Recorded", 3, 2, "2026-10-04", "2026-10-07", 500, "Giachi") + reasonLine, note?.Content);
            Assert.Equal("jail_release_note", note?.ItemTemplateId);
        }
    }
}
