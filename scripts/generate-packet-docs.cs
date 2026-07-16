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

// Packet families: every packet class must belong to exactly one family, so
// adding a packet without categorizing it (or renaming a class) breaks the
// generation instead of silently dropping the page from the family lists.
var families = new Family[]
{
    new("login-shard-select", "Login & shard select",
        "Seed, account auth, server list, shard select, game-server handoff.",
        ["LoginSeedPacket", "AccountLoginRequestPacket", "LoginDeniedPacket", "ServerListPacket", "SelectServerPacket", "ConnectToGameServerPacket", "GameServerLoginPacket", "ClientVersionPacket"]),
    new("characters", "Characters",
        "Character list, creation, selection, deletion and list updates.",
        ["CharacterListPacket", "CharacterCreationPacket", "CharacterSelectPacket", "DeleteCharacterPacket", "CharacterDeleteResultPacket", "CharacterListUpdatePacket"]),
    new("enter-world", "Enter world",
        "Login confirm, feature flags, and the login-complete marker.",
        ["LoginConfirmPacket", "SupportFeaturesPacket", "LoginCompletePacket"]),
    new("world-state", "World state",
        "Light levels, game time, season, map change and map patches.",
        ["PersonalLightLevelPacket", "OverallLightLevelPacket", "GameTimePacket", "SeasonChangePacket", "MapChangePacket", "MapPatchesPacket"]),
    new("movement", "Movement",
        "Move requests, acks, and mobile position updates.",
        ["MoveRequestPacket", "MovementAckPacket", "MobileUpdatePacket", "MobileIncomingPacket"]),
    new("status-skills", "Status & skills",
        "Mobile status, paperdoll, skills, war mode, stat/skill locks.",
        ["MobileStatusPacket", "PaperdollPacket", "SkillsPacket", "SkillLockChangePacket", "StatLockInfoPacket", "WarModePacket"]),
    new("interaction-keepalive", "Interaction & keepalive",
        "Single/double click, the 0xBF request multiplexer, and ping round-trips.",
        ["DoubleClickPacket", "SingleClickPacket", "GeneralInformationPacket", "PingPacket", "PingAckPacket"]),
};

var byClass = packets.ToDictionary(p => p.ClassName, StringComparer.Ordinal);
var assigned = new HashSet<string>(StringComparer.Ordinal);

foreach (var family in families)
{
    foreach (var className in family.Classes)
    {
        if (!byClass.ContainsKey(className))
        {
            errors.Add($"family '{family.Slug}' references unknown class '{className}' — was it renamed?");
        }

        if (!assigned.Add(className))
        {
            errors.Add($"class '{className}' is assigned to more than one family");
        }
    }
}

foreach (var packet in packets.Where(p => !assigned.Contains(p.ClassName)))
{
    errors.Add($"class '{packet.ClassName}' is not assigned to any family in scripts/generate-packet-docs.cs");
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

var familyDocs = families
    .Select(f => new FamilyDoc(
        f,
        f.Classes
            .Select(c => byClass[c])
            .OrderBy(p => p.OpcodeValue)
            .ThenBy(p => p.Direction, StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList()))
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

    var fields = (record.ParameterList?.Parameters ?? default)
        .Select(p => (Name: p.Identifier.Text, Type: p.Type?.ToString() ?? "?"))
        .ToList();

    var name = DisplayName(className);
    var slug = $"{opcode.ToLowerInvariant()}-{name.ToLowerInvariant().Replace(' ', '-')}";
    var sourcePath = file.Replace(Path.DirectorySeparatorChar, '/');

    return new PacketDoc(
        className, direction, opcode, opcodeValue, name, slug, summary,
        ShortDescription(summary), ExtractSize(summary), fields, sourcePath);
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

static string ExtractSize(string summary)
{
    var match = Regex.Match(summary, @"(\d+)\s+bytes?\s+fixed", RegexOptions.IgnoreCase);

    if (match.Success)
    {
        return $"{match.Groups[1].Value} bytes (fixed)";
    }

    return Regex.IsMatch(summary, "variable length", RegexOptions.IgnoreCase) ? "Variable" : "—";
}

static string ShortDescription(string summary)
{
    // Drop the "Name (0xNN):" prefix the summaries start with; the table has
    // dedicated opcode and name columns already.
    var text = Regex.Replace(summary, @"^[^:]{0,60}\(0x[0-9A-Fa-f]{2}\):\s*", "");
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

    sb.Append($"# {packet.Opcode} — {packet.Name}\n\n");
    sb.Append($"<span class=\"mg-dir {dirClass}\">{dirLabel}</span>\n\n");
    sb.Append(packet.Summary).Append("\n\n");
    sb.Append($"- **Class:** [`{packet.ClassName}`]({GitHubBlobRoot}{packet.SourcePath})\n");
    sb.Append($"- **Size:** {packet.Size}\n\n");
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
        sb.Append($"| [`{packet.Opcode}`]({href}) | {packet.Name} | {dir} | {packet.Size} | {packet.ShortDescription} |\n");
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
            sb.Append($"    - name: \"{packet.Opcode} — {packet.Name}\"\n      href: {folder}/{packet.Slug}.md\n");
        }
    }
}

internal sealed record Family(string Slug, string Title, string Description, string[] Classes);

internal sealed record FamilyDoc(Family Family, List<PacketDoc> Members);

internal sealed record PacketDoc(
    string ClassName,
    string Direction,
    string Opcode,
    int OpcodeValue,
    string Name,
    string Slug,
    string Summary,
    string ShortDescription,
    string Size,
    List<(string Name, string Type)> Fields,
    string SourcePath);
