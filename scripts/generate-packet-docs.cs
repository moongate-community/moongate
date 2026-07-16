#:package Microsoft.CodeAnalysis.CSharp@4.14.0

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const string ClientTarget = "7.x";
const string GitHubBlobRoot = "https://github.com/moongate-community/moongate/blob/main/";

var packetsRoot = Path.Combine("src", "Moongate.Network", "Packets");
var outRoot = Path.Combine("docs", "packets");

if (!Directory.Exists(packetsRoot))
{
    Console.Error.WriteLine($"error: '{packetsRoot}' not found. Run this script from the repository root.");
    return 1;
}

var packets = new List<PacketDoc>();
var errors = new List<string>();

foreach (var direction in new[] { "Incoming", "Outgoing" })
{
    var dir = Path.Combine(packetsRoot, direction);

    foreach (var file in Directory.EnumerateFiles(dir, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
    {
        try
        {
            packets.Add(Parse(file, direction));
        }
        catch (Exception ex)
        {
            errors.Add($"{file}: {ex.Message}");
        }
    }
}

var duplicate = packets
    .GroupBy(p => (p.Direction, p.Slug))
    .FirstOrDefault(g => g.Count() > 1);

if (duplicate is not null)
{
    errors.Add($"duplicate page slug '{duplicate.Key.Slug}' in {duplicate.Key.Direction}");
}

// Packet families come from the [PacketDocumentation(PacketFamilyType.X)]
// attribute on each packet record (enforced in Parse). This map only holds
// the presentation strings per PacketFamilyType member; a packet using a
// member missing here breaks the generation.
var familyInfos = new FamilyInfo[]
{
    new("LoginShardSelect", "login-shard-select", "Login & shard select",
        "Seed, account auth, server list, shard select, game-server handoff."),
    new("Characters", "characters", "Characters",
        "Character list, creation, selection, deletion and list updates."),
    new("EnterWorld", "enter-world", "Enter world",
        "Login confirm, feature flags, and the login-complete marker."),
    new("WorldState", "world-state", "World state",
        "Light levels, game time, season, map change/patches, object removal."),
    new("ItemsContainers", "items-containers", "Items & containers",
        "World items, worn items, container gumps, contents and lift rejects."),
    new("Movement", "movement", "Movement",
        "Move requests, acks, and mobile position updates."),
    new("StatusSkills", "status-skills", "Status & skills",
        "Mobile status, paperdoll, skills, war mode, stat/skill locks."),
    new("InteractionKeepalive", "interaction-keepalive", "Interaction & keepalive",
        "Single/double click, the 0xBF request multiplexer, and ping round-trips."),
};

var knownFamilies = familyInfos.Select(f => f.Member).ToHashSet(StringComparer.Ordinal);

foreach (var packet in packets.Where(p => !knownFamilies.Contains(p.Family)))
{
    errors.Add(
        $"packet '{packet.ClassName}' uses family '{packet.Family}' which has no entry in the familyInfos map in this script");
}

if (errors.Count > 0)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"error: {error}");
    }

    return 1;
}

packets = packets
    .OrderBy(p => p.OpcodeValue)
    .ThenBy(p => p.Direction, StringComparer.Ordinal)
    .ThenBy(p => p.Name, StringComparer.Ordinal)
    .ToList();

// The generator owns these paths and rebuilds them from scratch every run.
// It must never touch docs/packets/index.md (hand-written).
foreach (var sub in new[] { "incoming", "outgoing", "families", "includes" })
{
    var dir = Path.Combine(outRoot, sub);

    if (Directory.Exists(dir))
    {
        Directory.Delete(dir, recursive: true);
    }

    Directory.CreateDirectory(dir);
}

foreach (var packet in packets)
{
    var path = Path.Combine(outRoot, packet.Direction.ToLowerInvariant(), packet.Slug + ".md");
    File.WriteAllText(path, PacketPage(packet));
}

// packets is already sorted; empty families (enum member without packets yet)
// simply get no page.
var familyDocs = familyInfos
    .Select(info => new FamilyDoc(info, packets.Where(p => p.Family == info.Member).ToList()))
    .Where(f => f.Members.Count > 0)
    .ToList();

foreach (var familyDoc in familyDocs)
{
    File.WriteAllText(Path.Combine(outRoot, "families", familyDoc.Family.Slug + ".md"), FamilyPage(familyDoc));
}

File.WriteAllText(Path.Combine(outRoot, "includes", "packet-table.md"), TableInclude(packets));
File.WriteAllText(Path.Combine(outRoot, "includes", "family-cards.md"), FamilyCards(familyDocs));
File.WriteAllText(Path.Combine(outRoot, "toc.yml"), Toc(packets, familyDocs));

var incomingCount = packets.Count(p => p.Direction == "Incoming");
Console.WriteLine(
    $"Generated {packets.Count} packet pages ({incomingCount} incoming, {packets.Count - incomingCount} outgoing) + {familyDocs.Count} family pages + includes + toc.yml");
return 0;

static PacketDoc Parse(string file, string direction)
{
    var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetCompilationUnitRoot();

    var records = root.DescendantNodes().OfType<RecordDeclarationSyntax>().ToList();

    if (records.Count != 1)
    {
        throw new InvalidOperationException($"expected exactly 1 record declaration, found {records.Count}");
    }

    var record = records[0];
    var className = record.Identifier.Text;
    var opcode = FindOpcode(record) ?? throw new InvalidOperationException("no PacketId const or expression-bodied property found");

    if (!opcode.StartsWith("0x", StringComparison.Ordinal) ||
        !int.TryParse(opcode[2..], System.Globalization.NumberStyles.HexNumber, null, out var opcodeValue))
    {
        throw new InvalidOperationException($"PacketId '{opcode}' is not a 0x.. hex literal");
    }

    var summary = ExtractSummary(record) ?? throw new InvalidOperationException("no XML <summary> doc comment found");
    var doc = FindDocumentation(record)
              ?? throw new InvalidOperationException("no [PacketDocumentation(PacketFamilyType.X, ...)] attribute found");

    if (doc.Family is null)
    {
        throw new InvalidOperationException("[PacketDocumentation] is missing the PacketFamilyType argument");
    }

    if (doc.Length > 0 == doc.IsVariableLength)
    {
        throw new InvalidOperationException("[PacketDocumentation] must declare exactly one of Length or IsVariableLength");
    }

    var fields = (record.ParameterList?.Parameters ?? default)
        .Select(p => (Name: p.Identifier.Text, Type: p.Type?.ToString() ?? "?"))
        .ToList();

    var name = doc.Name ?? DisplayName(className);
    var slug = $"{opcode.ToLowerInvariant()}-{name.ToLowerInvariant().Replace(' ', '-')}";
    var sourcePath = file.Replace(Path.DirectorySeparatorChar, '/');
    var size = doc.IsVariableLength ? "Variable" : $"{doc.Length} bytes (fixed)";
    var subCommand = doc.SubCommand >= 0 ? $"0x{doc.SubCommand:X2}" : null;
    var opcodeDisplay = subCommand is null ? opcode : $"{opcode}/{subCommand}";

    return new PacketDoc(
        className, direction, opcode, opcodeValue, opcodeDisplay, name, slug, summary,
        ShortDescription(summary), size, subCommand, fields, sourcePath, doc.Family);
}

static PacketAttributeInfo? FindDocumentation(RecordDeclarationSyntax record)
{
    foreach (var attribute in record.AttributeLists.SelectMany(l => l.Attributes))
    {
        var attributeName = attribute.Name.ToString();

        if (attributeName is not ("PacketDocumentation" or "PacketDocumentationAttribute"))
        {
            continue;
        }

        string? family = null;
        string? name = null;
        var length = -1;
        var subCommand = -1;
        var isVariableLength = false;

        foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
        {
            var text = argument.Expression.ToString();

            switch (argument.NameEquals?.Name.Identifier.Text)
            {
                case null:
                    family = text.Split('.')[^1];
                    break;
                case "Length":
                    length = ParseIntLiteral(text);
                    break;
                case "IsVariableLength":
                    isVariableLength = text == "true";
                    break;
                case "SubCommand":
                    subCommand = ParseIntLiteral(text);
                    break;
                case "Name":
                    name = text.Trim('"');
                    break;
            }
        }

        return new PacketAttributeInfo(family, length, isVariableLength, subCommand, name);
    }

    return null;
}

static int ParseIntLiteral(string text)
{
    return text.StartsWith("0x", StringComparison.Ordinal)
        ? Convert.ToInt32(text[2..], 16)
        : int.Parse(text);
}

static string? FindOpcode(RecordDeclarationSyntax record)
{
    foreach (var member in record.Members)
    {
        if (member is FieldDeclarationSyntax field)
        {
            var declarator = field.Declaration.Variables.FirstOrDefault(v => v.Identifier.Text == "PacketId");

            if (declarator?.Initializer is not null)
            {
                return declarator.Initializer.Value.ToString();
            }
        }

        if (member is PropertyDeclarationSyntax { Identifier.Text: "PacketId", ExpressionBody: not null } property)
        {
            return property.ExpressionBody.Expression.ToString();
        }
    }

    return null;
}

static string? ExtractSummary(RecordDeclarationSyntax record)
{
    var raw = record.GetLeadingTrivia()
        .Select(t => t.GetStructure())
        .OfType<DocumentationCommentTriviaSyntax>()
        .FirstOrDefault()
        ?.ToFullString();

    if (raw is null)
    {
        return null;
    }

    var match = Regex.Match(raw, "<summary>(.*?)</summary>", RegexOptions.Singleline);

    if (!match.Success)
    {
        return null;
    }

    var text = match.Groups[1].Value;
    text = Regex.Replace(text, @"^\s*///", "", RegexOptions.Multiline);
    text = Regex.Replace(text, @"<see\s+cref=""([^""]+)""\s*/>", m => $"`{m.Groups[1].Value.Split('.')[^1]}`");
    text = Regex.Replace(text, "<c>(.*?)</c>", "`$1`", RegexOptions.Singleline);
    text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
    text = Regex.Replace(text, @"\s+", " ").Trim();

    return text.Length == 0 ? null : text;
}

static string DisplayName(string className)
{
    var baseName = className.EndsWith("Packet", StringComparison.Ordinal)
        ? className[..^"Packet".Length]
        : className;

    return Regex.Replace(baseName, "(?<=[a-z0-9])(?=[A-Z])", " ");
}

static string ShortDescription(string summary)
{
    // Drop the "Name (0xNN):" / "Name (0xBF sub-command 0xNN):" prefix the
    // summaries start with; the table has dedicated opcode and name columns.
    var text = Regex.Replace(summary, @"^[^:]{0,60}\(0x[0-9A-Fa-f]{2}[^)]*\):\s*", "");
    var sentenceEnd = text.IndexOf(". ", StringComparison.Ordinal);

    if (sentenceEnd > 0)
    {
        text = text[..(sentenceEnd + 1)];
    }

    return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

static string PacketPage(PacketDoc packet)
{
    var dirLabel = packet.Direction == "Incoming" ? "Client → Server" : "Server → Client";
    var dirClass = packet.Direction == "Incoming" ? "mg-dir-in" : "mg-dir-out";
    var sb = new StringBuilder();

    sb.Append($"# {packet.OpcodeDisplay} — {packet.Name}\n\n");
    sb.Append($"<span class=\"mg-dir {dirClass}\">{dirLabel}</span>\n\n");
    sb.Append(packet.Summary).Append("\n\n");
    sb.Append($"- **Class:** [`{packet.ClassName}`]({GitHubBlobRoot}{packet.SourcePath})\n");
    sb.Append($"- **Size:** {packet.Size}\n");

    if (packet.SubCommand is not null)
    {
        sb.Append($"- **Sub-command:** `{packet.SubCommand}` of General Information (`{packet.Opcode}`)\n");
    }

    sb.Append('\n');
    sb.Append("## Fields\n\n");

    if (packet.Fields.Count == 0)
    {
        sb.Append("This packet carries no fields beyond its opcode.\n");
    }
    else
    {
        sb.Append("| Field | Type |\n|---|---|\n");

        foreach (var field in packet.Fields)
        {
            sb.Append($"| `{field.Name}` | `{field.Type}` |\n");
        }
    }

    return sb.ToString();
}

static string TableInclude(List<PacketDoc> packets)
{
    var incoming = packets.Count(p => p.Direction == "Incoming");
    var outgoing = packets.Count - incoming;
    var sb = new StringBuilder();

    sb.Append("<div class=\"mg-stats\">\n");
    sb.Append($"  <div class=\"mg-stat\"><div class=\"mg-stat-num\">{packets.Count}</div><div class=\"mg-stat-label\">implemented packets</div></div>\n");
    sb.Append($"  <div class=\"mg-stat\"><div class=\"mg-stat-num mg-grass\">{incoming}</div><div class=\"mg-stat-label\">incoming (client → server)</div></div>\n");
    sb.Append($"  <div class=\"mg-stat\"><div class=\"mg-stat-num mg-violet\">{outgoing}</div><div class=\"mg-stat-label\">outgoing (server → client)</div></div>\n");
    sb.Append($"  <div class=\"mg-stat\"><div class=\"mg-stat-num mg-stone\">{ClientTarget}</div><div class=\"mg-stat-label\">client target</div></div>\n");
    sb.Append("</div>\n\n");

    sb.Append(PacketTable(packets));

    return sb.ToString();
}

static string PacketTable(IEnumerable<PacketDoc> packets)
{
    var sb = new StringBuilder();
    sb.Append("| Opcode | Name | Dir | Size | Description |\n");
    sb.Append("|---|---|---|---|---|\n");

    foreach (var packet in packets)
    {
        var dir = packet.Direction == "Incoming" ? "C → S" : "S → C";
        var href = $"../{packet.Direction.ToLowerInvariant()}/{packet.Slug}.md";
        sb.Append($"| [`{packet.OpcodeDisplay}`]({href}) | {packet.Name} | {dir} | {packet.Size} | {packet.ShortDescription} |\n");
    }

    return sb.ToString();
}

static string FamilyPage(FamilyDoc familyDoc)
{
    var sb = new StringBuilder();
    sb.Append($"# {familyDoc.Family.Title}\n\n");
    sb.Append(familyDoc.Family.Description).Append("\n\n");
    sb.Append(PacketTable(familyDoc.Members));

    return sb.ToString();
}

static string FamilyCards(List<FamilyDoc> familyDocs)
{
    var sb = new StringBuilder();
    sb.Append("<div class=\"mg-cards mg-cards-article\">\n");

    foreach (var familyDoc in familyDocs)
    {
        // DocFX resolves raw HTML hrefs relative to the page that includes
        // this file (docs/packets/index.md), rewriting .md to .html.
        var opcodes = string.Join(" · ", familyDoc.Members.Select(p => p.Opcode).Distinct());
        sb.Append($"  <a class=\"mg-card\" href=\"families/{familyDoc.Family.Slug}.md\">\n");
        sb.Append($"    <h3>{HtmlEscape(familyDoc.Family.Title)}</h3>\n");
        sb.Append($"    <div class=\"mg-card-ops\">{opcodes}</div>\n");
        sb.Append($"    <p>{HtmlEscape(familyDoc.Family.Description)}</p>\n");
        sb.Append("  </a>\n");
    }

    sb.Append("</div>\n");

    return sb.ToString();
}

static string HtmlEscape(string text)
{
    return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

static string Toc(List<PacketDoc> packets, List<FamilyDoc> familyDocs)
{
    var sb = new StringBuilder();
    sb.Append("- name: Overview\n  href: index.md\n");
    sb.Append("- name: Families\n  items:\n");

    foreach (var familyDoc in familyDocs)
    {
        sb.Append($"    - name: \"{familyDoc.Family.Title}\"\n      href: families/{familyDoc.Family.Slug}.md\n");
    }

    AppendGroup(sb, "Incoming (client → server)", "incoming", packets.Where(p => p.Direction == "Incoming"));
    AppendGroup(sb, "Outgoing (server → client)", "outgoing", packets.Where(p => p.Direction == "Outgoing"));

    return sb.ToString();

    static void AppendGroup(StringBuilder sb, string title, string folder, IEnumerable<PacketDoc> group)
    {
        sb.Append($"- name: {title}\n  items:\n");

        foreach (var packet in group)
        {
            sb.Append($"    - name: \"{packet.OpcodeDisplay} — {packet.Name}\"\n      href: {folder}/{packet.Slug}.md\n");
        }
    }
}

internal sealed record PacketAttributeInfo(
    string? Family,
    int Length,
    bool IsVariableLength,
    int SubCommand,
    string? Name);

internal sealed record FamilyInfo(string Member, string Slug, string Title, string Description);

internal sealed record FamilyDoc(FamilyInfo Family, List<PacketDoc> Members);

internal sealed record PacketDoc(
    string ClassName,
    string Direction,
    string Opcode,
    int OpcodeValue,
    string OpcodeDisplay,
    string Name,
    string Slug,
    string Summary,
    string ShortDescription,
    string Size,
    string? SubCommand,
    List<(string Name, string Type)> Fields,
    string SourcePath,
    string Family);
