using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Services.Text;
using Moongate.Server.Ultima.Types.Text;
using Moongate.UoxItemConverter.Internal;
using Moongate.UoxItemConverter.Tests.TestSupport;

namespace Moongate.UoxItemConverter.Tests.Integration;

public sealed class ModernUoBookConverterTests : IDisposable
{
    private readonly ConverterTestDirectories _directories = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    [Fact]
    public void Run_LiteralCatalog_PreservesTitleAuthorLinesBlankPagesAndDistinctJournalParts()
    {
        _directories.WriteSource("Defined/Journal.cs", """
            namespace Server.Items;
            public class Journal1 : BaseBook
            {
                public static readonly BookContent Content = new("Journal", "Writer",
                    new BookPageInfo(" first", "", "last"), new BookPageInfo(), new BookPageInfo("end"));
            }
            public class Journal2 : BaseBook
            {
                public static readonly BookContent Content = new BookContent("Journal", "Writer",
                    new BookPageInfo("another part"));
            }
            public class BlankBook : BaseBook { public string Title = "no fixed content"; }
            """);

        Assert.True(Run() == 0, _error.ToString());
        var first = Read("modernuo_journal1");
        Assert.Equal("Journal", first.Title);
        Assert.Equal("Writer", first.Author);
        Assert.Equal(" first\n\nlast\n\n\n\nend", first.Content);
        Assert.Equal("another part", Read("modernuo_journal2").Content);
        Assert.Equal("readable_scroll", first.ItemTemplate);
        Assert.Empty(first.Variables);
        Assert.Empty(first.Attachments);
        Assert.Empty(first.Translations);
        Assert.Contains("content = \"\"\"", File.ReadAllText(Path.Combine(_directories.DestinationDirectory, "modernuo_journal1.toml")));
        Assert.Equal(2, Directory.GetFiles(_directories.DestinationDirectory).Length);
        Assert.Contains("2 books, 4 pages", _output.ToString());
    }

    [Fact]
    public void Run_StringLiteralsAndComments_DecodeWithoutTreatingLiteralDollarsAsVariables()
    {
        _directories.WriteSource("Escapes.cs", """"
            // BookContent Content = new("fake", "fake", new BookPageInfo("fake"));
            class UOJournal16b
            {
                public static readonly BookContent Content = new(
                    "A \"quoted\" title", @"A\B", new BookPageInfo(
                        "caf\u00e8", "price $5; $player_name; ${unknown}",
                        "http://example.test/" + "path", """raw \ text"""));
            }
            """");

        Assert.True(Run() == 0, _error.ToString());
        var book = Read("modernuo_uo_journal16b");
        Assert.Equal("A \"quoted\" title", book.Title);
        Assert.Equal("A\\B", book.Author);
        Assert.Equal("cafè\nprice $$5; $$player_name; $${unknown}\nhttp://example.test/path\nraw \\ text", book.Content);
        Assert.Equal("cafè\nprice $5; $player_name; ${unknown}\nhttp://example.test/path\nraw \\ text",
            TextTemplateRenderer.Render(book.Content, new Dictionary<string, string>(), TextTemplateSyntaxType.Document));
    }

    [Theory]
    [InlineData("new(GetTitle(), \"Writer\", new BookPageInfo(\"text\"))")]
    [InlineData("new(\"Title\", \"Writer\", new BookPageInfo(GetText()))")]
    [InlineData("new(\"Title\", \"Writer\", new OtherPage(\"text\"))")]
    [InlineData("new(\"Title\", \"Writer\", new BookPageInfo($\"{GetText()}\"))")]
    [InlineData("new(\"Title\", \"Writer\")")]
    [InlineData("new(\"Title\", \"Writer\", new BookPageInfo(\"text\")) garbage")]
    public void Run_UnsupportedOrMalformedCatalog_RejectsBeforeChangingEarlierOutput(string initializer)
    {
        _directories.WriteSource("A.cs", Book("Earlier", "kept"));
        Assert.True(Run() == 0, _error.ToString());
        var path = Path.Combine(_directories.DestinationDirectory, "modernuo_earlier.toml");
        var previous = File.ReadAllBytes(path);
        _directories.WriteSource("A.cs", Book("Earlier", "changed"));
        _directories.WriteSource("Z.cs", "class Bad { public static readonly BookContent Content = " + initializer + "; }");

        Assert.Equal(2, Run());
        Assert.Contains("Z.cs", _error.ToString());
        Assert.Equal(previous, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_directories.DestinationDirectory));
    }

    [Theory]
    [InlineData("", "Writer", "text")]
    [InlineData("Title", "Writer", "")]
    [InlineData("Title", "Writer", "bad\u0000text")]
    public void Run_InvalidDocumentText_RejectsWithoutCreatingDestination(string title, string author, string content)
    {
        _directories.WriteSource("Bad.cs", $"class Bad {{ public static readonly BookContent Content = new({Literal(title)}, {Literal(author)}, new BookPageInfo({Literal(content)})); }}");

        Assert.Equal(2, Run());
        Assert.Contains("Bad.cs", _error.ToString());
        Assert.False(Directory.Exists(_directories.DestinationDirectory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_OversizedHeaderOrBody_RejectsWithoutWriting(bool header)
    {
        var title = header ? new string('x', 129) : "Title";
        var content = header ? "text" : new string('x', 16385);
        _directories.WriteSource("Large.cs", $"class Large {{ public static readonly BookContent Content = new({Literal(title)}, \"Writer\", new BookPageInfo({Literal(content)})); }}");

        Assert.Equal(2, Run());
        Assert.Contains("Large.cs", _error.ToString());
        Assert.False(Directory.Exists(_directories.DestinationDirectory));
    }

    [Fact]
    public void Run_TextThatOverflowsTheEscapedReadingPacket_RejectsBeforeWriting()
    {
        _directories.WriteSource("Large.cs", Book("Large", new string('&', 16000)));

        Assert.Equal(2, Run());
        Assert.Contains("Large.cs", _error.ToString());
        Assert.False(Directory.Exists(_directories.DestinationDirectory));
    }

    [Theory]
    [InlineData(8193)]
    [InlineData(9000)]
    public void Run_DollarEscapingExceedsSourceLimit_RejectsBeforeChangingEarlierOutput(int count)
    {
        _directories.WriteSource("A.cs", Book("Earlier", "kept"));
        Assert.Equal(0, Run());
        var path = Path.Combine(_directories.DestinationDirectory, "modernuo_earlier.toml");
        var previous = File.ReadAllBytes(path);
        _directories.WriteSource("A.cs", Book("Earlier", "changed"));
        _directories.WriteSource("Z.cs", Book("Dollars", new string('$', count)));

        Assert.Equal(2, Run());
        Assert.Equal(previous, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_directories.DestinationDirectory));
    }

    [Fact]
    public void Run_DollarEscapingAtSourceLimit_PreservesRenderedText()
    {
        var content = new string('$', 8192);
        _directories.WriteSource("A.cs", Book("Dollars", content));

        Assert.True(Run() == 0, _error.ToString());
        Assert.Equal(16384, Read("modernuo_dollars").Content.Length);
        Assert.Equal(content, TextTemplateRenderer.Render(Read("modernuo_dollars").Content,
            new Dictionary<string, string>(), TextTemplateSyntaxType.Document));
    }

    [Theory]
    [InlineData("\n\nfirst")]
    [InlineData("a\rb\r\nc")]
    [InlineData("\nfirst")]
    [InlineData("face \U0001F600")]
    public void Run_LeadingBlankPagesAndLineEndings_PreservesDecodedText(string content)
    {
        _directories.WriteSource("A.cs", Book("Special", content));

        Assert.True(Run() == 0, _error.ToString());
        Assert.Equal(content, Read("modernuo_special").Content);
    }

    [Theory]
    [InlineData("\\uD800", "Title", "Writer")]
    [InlineData("text", "\\uD800", "Writer")]
    [InlineData("text", "Title", "\\uDC00")]
    public void Run_InvalidUnicode_RejectsBeforeChangingEarlierOutput(string content, string title, string author)
    {
        _directories.WriteSource("A.cs", Book("Earlier", "kept"));
        Assert.Equal(0, Run());
        var path = Path.Combine(_directories.DestinationDirectory, "modernuo_earlier.toml");
        var previous = File.ReadAllBytes(path);
        _directories.WriteSource("A.cs", Book("Earlier", "changed"));
        _directories.WriteSource("Z.cs", $"class Bad {{ public static readonly BookContent Content = new(\"{title}\", \"{author}\", new BookPageInfo(\"{content}\")); }}");

        Assert.Equal(2, Run());
        Assert.Equal(previous, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(_directories.DestinationDirectory));
    }

    [Fact]
    public void Run_CollidingClassIds_RejectsBothInsteadOfOverwriting()
    {
        _directories.WriteSource("A.cs", Book("MyBook", "first"));
        _directories.WriteSource("B.cs", Book("My_Book", "second"));

        Assert.Equal(2, Run());
        Assert.Contains("duplicate", _error.ToString());
        Assert.False(Directory.Exists(_directories.DestinationDirectory));
    }

    [Fact]
    public void Run_RepeatedConversion_IsDeterministicAndPreservesUnrelatedFiles()
    {
        _directories.WriteSource("A.cs", Book("Known", "first"));
        Assert.True(Run() == 0, _error.ToString());
        var path = Path.Combine(_directories.DestinationDirectory, "modernuo_known.toml");
        var before = File.ReadAllBytes(path);
        var unrelated = Path.Combine(_directories.DestinationDirectory, "welcome_letter.toml");
        File.WriteAllText(unrelated, "custom letter");

        Assert.True(Run() == 0, _error.ToString());
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal("custom letter", File.ReadAllText(unrelated));
        _directories.WriteSource("A.cs", Book("Known", "second"));
        Assert.True(Run() == 0, _error.ToString());
        Assert.Equal("second", Read("modernuo_known").Content);
        Assert.Equal("custom letter", File.ReadAllText(unrelated));
    }

    [Fact]
    public void Run_OutputFilenameOccupiedByDirectory_RejectsBeforeChangingOtherBooks()
    {
        _directories.WriteSource("A.cs", Book("First", "kept"));
        Assert.True(Run() == 0, _error.ToString());
        _directories.WriteSource("A.cs", Book("First", "changed"));
        _directories.WriteSource("Z.cs", Book("Last", "new"));
        Directory.CreateDirectory(Path.Combine(_directories.DestinationDirectory, "modernuo_last.toml"));

        Assert.Equal(2, Run());
        Assert.Equal("kept", Read("modernuo_first").Content);
        Assert.Contains("modernuo_last.toml", _error.ToString());
    }

    [Fact]
    public void Run_OutputFileSymlink_RefusesToOverwriteItsTarget()
    {
        if (OperatingSystem.IsWindows()) return;
        _directories.WriteSource("A.cs", Book("Known", "new"));
        Directory.CreateDirectory(_directories.DestinationDirectory);
        var target = _directories.WriteSource("outside.txt", "preserved");
        File.CreateSymbolicLink(Path.Combine(_directories.DestinationDirectory, "modernuo_known.toml"), target);

        Assert.Equal(2, Run());
        Assert.Equal("preserved", File.ReadAllText(target));
    }

    [Fact]
    public void Run_EmptyOrMissingSource_ReportsUsageErrorWithoutDestination()
    {
        Assert.Equal(2, Run());
        Assert.Contains("no static books", _error.ToString());
        Directory.Delete(_directories.SourceDirectory);
        Assert.Equal(2, Run());
        Assert.Contains("does not exist", _error.ToString());
        Assert.False(Directory.Exists(_directories.DestinationDirectory));
    }

    private static string Book(string name, string text)
    {
        return $"class {name} {{ public static readonly BookContent Content = new(\"Title\", \"Writer\", new BookPageInfo({Literal(text)})); }}";
    }

    private static string Literal(string text)
    {
        return System.Text.Json.JsonSerializer.Serialize(text);
    }

    private int Run()
    {
        return ModernUoBookConverter.Run(_directories.SourceDirectory, _directories.DestinationDirectory, _output, _error);
    }

    private BookTemplateSource Read(string id)
    {
        return Assert.IsType<BookTemplateSource>(TomlUtils.DeserializeFromFile<BookTemplateSource>(
            Path.Combine(_directories.DestinationDirectory, id + ".toml")));
    }

    public void Dispose()
    {
        _directories.Dispose();
        _output.Dispose();
        _error.Dispose();
    }
}
