using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Tests.TestSupport.Ctl;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Ctl;

public sealed class BookConverterCommandTests
{
    [Fact]
    public async Task Run_StaticBook_WritesAReadableTomlThroughTheActualCommand()
    {
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Sample.cs"), """
            class SampleBook
            {
                public static readonly BookContent Content = new("Title", "Writer",
                    new BookPageInfo("First line", "Second line"));
            }
            """);
        var destination = Path.Combine(directory.Path, "books");

        var result = await CtlProcess.RunAsync("convert", "modernuo-books", "--source", source, "--destination", destination);

        Assert.True(result.ExitCode == 0, result.Output);
        var book = TomlUtils.DeserializeFromFile<BookTemplateSource>(Path.Combine(destination, "sample_book.toml"));
        Assert.Equal("First line\nSecond line", book!.Content);
        Assert.Contains("1 books, 1 pages", result.Output);
    }

    [Fact]
    public async Task Run_Help_DescribesTheSourceAndOutputFolder()
    {
        var result = await CtlProcess.RunAsync("convert", "modernuo-books", "--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--source", result.Output);
        Assert.Contains("--destination", result.Output);
    }
}
