using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Core.Serialization.Toml;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

public sealed class BookUpgradeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingRoot_DocumentedMergePreservesCustomizationsAndReadsNewAndLegacyNotes(bool legacy)
    {
        using var directory = new TemporaryDirectory();
        var repository = BookLuaFixture.RepositoryRoot();
        var distribution = Path.Combine(repository, "moongate_root");
        var root = Path.Combine(directory.Path, "root");
        var oldTemplate = (await File.ReadAllTextAsync(Path.Combine(distribution, "templates/items/jail.toml")))
            .Replace("stackable = false\n", "", StringComparison.Ordinal)
            .Replace("a chest of rations", "our custom chest", StringComparison.Ordinal);
        var templatePath = directory.CreateFile("root/templates/items/jail.toml", oldTemplate);
        const string oldFunction = """
                                   function jail_note.on_use(serial, user)
                                       return item.get_prop(serial, "jail.text") ~= nil
                                   end
                                   """;
        var oldScript = "-- operator customization\njail_note = {}\n" + oldFunction +
                        "\nfunction jail_note.custom() return 42 end\n";
        var scriptPath = directory.CreateFile("root/scripts/items/jail_note.lua", oldScript);
        directory.CreateFile("migrations/auth/0001_base.sql", "SELECT 1;\n");
        RootDirectoryInitializer.Initialize(
            root,
            Path.Combine(directory.Path, "migrations"),
            Path.Combine(distribution, "data"),
            TextWriter.Null,
            templatesDirectory: Path.Combine(distribution, "templates"),
            scriptsDirectory: Path.Combine(distribution, "scripts")
        );
        Assert.Equal(oldTemplate, await File.ReadAllTextAsync(templatePath));
        Assert.Equal(oldScript, await File.ReadAllTextAsync(scriptPath));
        var directories = new DirectoriesConfig(root, ["templates"]);
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
        var oldItems = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        Assert.Null(oldItems.Single(item => item.Id == "jail_release_note").Stackable);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new BooksLoader(directories, new StubDataLoaderService().With(oldItems)).LoadDataAsync()
        );

        // Execute the two documented edits, rather than silently replacing the operator's files.
        var docs = await File.ReadAllTextAsync(Path.Combine(repository, "docs/data-files/books.md"));
        var upgradeStart = docs.IndexOf("### Existing roots", StringComparison.Ordinal);
        Assert.True(upgradeStart >= 0, "Existing-root upgrades need explicit instructions before the first server start.");
        var upgrade = docs[upgradeStart..];
        var field = CodeBlock(upgrade, "toml");
        await File.WriteAllTextAsync(
            templatePath,
            oldTemplate.Replace("script_id = \"jail_note\"", "script_id = \"jail_note\"\n" + field, StringComparison.Ordinal)
        );
        var mergedScript = oldScript.Replace(oldFunction, CodeBlock(upgrade, "lua"), StringComparison.Ordinal);
        await File.WriteAllTextAsync(scriptPath, mergedScript);
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        Assert.False(items.Single(item => item.Id == "jail_release_note").Stackable);
        Assert.Equal("our custom chest", items.Single(item => item.Id == "jail_chest").Name);
        // Every book template shipped with the server loads against the upgraded items; how many there are is not the
        // test's business, the two the upgrade is about are.
        var books = (await new BooksLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;
        Assert.Contains(books, book => book.Id == "jail_release_note");
        Assert.Contains(books, book => book.Id == "welcome_letter");
        Assert.Contains("-- operator customization", mergedScript);
        Assert.Contains("function jail_note.custom() return 42 end", mergedScript);
        await using var fixture = await BookLuaFixture.CreateAsync(jailScript: mergedScript);
        await fixture.Documents.OnLoopAsync(() =>
            {
                var note = fixture.Documents.Give();
                note.TemplateId = "jail_release_note";
                note.SetProp("jail.text", "Legacy <note>");
                if (legacy) note.RemoveProp("book.content");
                fixture.ItemScripts.Run(note, "on_use", 2L);
            }
        );
        await fixture.Documents.OnLoopAsync(() =>
            {
                var strings = Assert.Single(fixture.Documents.Gumps.Opened).Gump.Layout.Build().Strings;
                Assert.Contains(legacy ? "Legacy &lt;note&gt;" : "Dear Pippo,<br><br>Bring this to Vega.", strings);
            }
        );
        Assert.Empty(fixture.Errors);
    }

    private static string CodeBlock(string section, string language)
    {
        var fence = "```" + language + "\n";
        var start = section.IndexOf(fence, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing executable {language} upgrade snippet.");
        start += fence.Length;
        var end = section.IndexOf("\n```", start, StringComparison.Ordinal);
        Assert.True(end >= 0);
        return section[start..end];
    }
}
