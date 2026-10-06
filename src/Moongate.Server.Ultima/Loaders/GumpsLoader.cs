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
///     <c>on_click</c>, <c>id</c>, <c>page</c> or <c>open</c>, a button opening a gump that does not exist, mixed
///     text sources, ids used twice, or the same gump id twice stop the server with the file, the line and the reason.
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

        // Known only once every file is read.
        var ids = gumps.Select(gump => gump.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var gump in gumps)
        {
            foreach (var button in gump.Root.Descendants("button"))
            {
                if (button.Attribute("open")?.Value is { } target && !ids.Contains(target))
                {
                    throw new InvalidDataException(
                        $"{gump.File}: line {((IXmlLineInfo)button).LineNumber}: a button opens gump '{target}', which does not exist."
                    );
                }
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
        CheckRules(path, root);

        foreach (var button in root.Descendants("button"))
        {
            if (button.Attribute("on_click")?.Value is { } click && click.StartsWith("__", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{path}: line {((IXmlLineInfo)button).LineNumber}: on_click names starting with __ are reserved."
                );
            }
        }

        return new() { Id = (string)root.Attribute("id")!, File = path, Root = root };
    }

    /// <summary>
    ///     Checks a gump made at runtime, such as one built from Lua or with its slots filled, as a file is checked:
    ///     against the schema, the rules the schema cannot say, and the gumps its <c>open</c> buttons name.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     The gump breaks a rule; the message says which.
    /// </exception>
    public static void Validate(string source, XElement root, Func<string, bool> gumpExists)
    {
        var document = new XDocument(new XElement(root));
        document.Validate(
            Schema.Value,
            (_, args) => throw new InvalidDataException($"{source}: {args.Message}"),
            false
        );
        CheckRules(source, root);

        foreach (var button in root.Descendants("button"))
        {
            if (button.Attribute("open")?.Value is { } target && !gumpExists(target))
            {
                throw new InvalidDataException($"{source}: a button opens gump '{target}', which does not exist.");
            }
        }
    }

    private static void CheckRules(string path, XElement root)
    {
        foreach (var element in root.Descendants())
        {
            Check(path, element);
        }

        CheckIds(path, root);

        // A slot's pages follow the gump's; with pages of its own the gump would number them twice.
        if (root.Elements("page").Any() && root.Descendants("slot").FirstOrDefault() is { } slot)
        {
            throw new InvalidDataException(
                $"{path}: line {((IXmlLineInfo)slot).LineNumber}: a slot cannot be in a gump with pages."
            );
        }
    }

    // What XSD 1.0 cannot say.
    private static void Check(string path, XElement element)
    {
        string? reason = element.Name.LocalName switch
        {
            "button" when new[] { "on_click", "id", "page", "open" }.Count(name => element.Attribute(name) is not null) !=
                          1 =>
                "a button needs exactly one of on_click, id, page or open",
            "html" when element.Attribute("cliloc") is not null && element.Attribute("message") is not null =>
                "an html takes a cliloc or message, not both",
            "html" when element.Attribute("cliloc") is not null && !string.IsNullOrWhiteSpace(element.Value) =>
                "an html takes a cliloc or a text, not both",
            "html" when element.Attribute("color") is not null && element.Attribute("cliloc") is null =>
                "an html color needs a cliloc",
            "text" or "label_cropped" or "html" when element.Attribute("message") is not null &&
                                                     !string.IsNullOrWhiteSpace(element.Value) =>
                $"a {element.Name.LocalName} takes a message or a text, not both",
            _ => null
        };

        if (reason is not null)
        {
            throw new InvalidDataException($"{path}: line {((IXmlLineInfo)element).LineNumber}: {reason}.");
        }
    }

    // What the client answers with must be told apart, and a page button must lead somewhere.
    private static void CheckIds(string path, XElement root)
    {
        void Twice(string kind, IEnumerable<XElement> elements, string attribute)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var element in elements)
            {
                if (element.Attribute(attribute)?.Value is { } id && !seen.Add(id))
                {
                    throw new InvalidDataException(
                        $"{path}: line {((IXmlLineInfo)element).LineNumber}: {kind} {id} twice."
                    );
                }
            }
        }

        Twice("button id", root.Descendants("button"), "id");
        Twice("switch", root.Descendants().Where(element => element.Name.LocalName is "checkbox" or "radio"), "switch");
        Twice("entry", root.Descendants("text_entry"), "entry");

        var pages = root.Elements("page").Count();

        foreach (var button in root.Descendants("button"))
        {
            if ((int?)button.Attribute("page") is { } page && page > pages)
            {
                throw new InvalidDataException(
                    $"{path}: line {((IXmlLineInfo)button).LineNumber}: a button turns to page {page}, but the gump has {pages}."
                );
            }
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
