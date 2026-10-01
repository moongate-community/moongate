using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads every <c>*.xml</c> of <c>templates/gumps/</c>, checked against the gump schema built into the server (the
///     <c>gump.xsd</c> next to the files is for editors). A file the schema refuses, a button with not exactly one of
///     <c>on_click</c>, <c>id</c> or <c>page</c>, an html with both a cliloc and a message or a text, or the same id twice
///     stop the server with the file, the line and the reason.
/// </summary>
public sealed class GumpsLoader : IDataLoader<GumpTemplate>
{
    public const string SchemaResource = "Moongate.Gumps.gump.xsd";

    private static readonly Lazy<XmlSchemaSet> Schema = new(LoadSchema);

    private readonly ILogger _logger = Log.ForContext<GumpsLoader>();
    private readonly DirectoriesConfig _directoriesConfig;

    private string gumpsDirectoryPath => Path.Join(_directoriesConfig["templates"], "gumps");

    public GumpsLoader(DirectoriesConfig directoriesConfig)
    {
        _directoriesConfig = directoriesConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<DataLoaderResult<GumpTemplate>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var gumps = new List<GumpTemplate>();

        if (Directory.Exists(gumpsDirectoryPath))
        {
            foreach (var path in Directory.GetFiles(gumpsDirectoryPath, "*.xml").Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gump = Load(path);

                if (gumps.FirstOrDefault(other => other.Id == gump.Id) is { } same)
                {
                    throw new InvalidDataException($"{path}: gump '{gump.Id}' is already defined in {same.File}.");
                }

                gumps.Add(gump);
            }
        }

        _logger.Information("Found {Count} gumps", gumps.Count);

        return Task.FromResult(new DataLoaderResult<GumpTemplate> { Entities = gumps });
    }

    private static GumpTemplate Load(string path)
    {
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema, Schemas = Schema.Value, DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,

            // A root the schema does not declare, such as one in a namespace, is only a warning: it must stop the server.
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
        };
        settings.ValidationEventHandler += (_, args) =>
            throw new InvalidDataException(
                $"{path}: line {args.Exception.LineNumber}, column {args.Exception.LinePosition}: {args.Message}"
            );

        XDocument document;

        try
        {
            using var reader = XmlReader.Create(path, settings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"{path}: line {exception.LineNumber}: {exception.Message}", exception);
        }

        var root = document.Root!;

        foreach (var element in root.Descendants())
        {
            Check(path, element);
        }

        return new() { Id = (string)root.Attribute("id")!, File = path, Root = root };
    }

    // What XSD 1.0 cannot say.
    private static void Check(string path, XElement element)
    {
        string? reason = element.Name.LocalName switch
        {
            "button" when new[] { "on_click", "id", "page" }.Count(name => element.Attribute(name) is not null) != 1 =>
                "a button needs exactly one of on_click, id or page",
            "html" when element.Attribute("cliloc") is not null && element.Attribute("message") is not null =>
                "an html takes a cliloc or message, not both",
            "html" when element.Attribute("cliloc") is not null && !string.IsNullOrWhiteSpace(element.Value) =>
                "an html takes a cliloc or a text, not both",
            _ => null
        };

        if (reason is not null)
        {
            throw new InvalidDataException($"{path}: line {((IXmlLineInfo)element).LineNumber}: {reason}.");
        }
    }

    private static XmlSchemaSet LoadSchema()
    {
        using var stream = typeof(GumpsLoader).Assembly.GetManifestResourceStream(SchemaResource)!;
        var schemas = new XmlSchemaSet();
        schemas.Add(null, XmlReader.Create(stream));
        schemas.Compile();

        return schemas;
    }
}
