using System.Text;
using Moongate.Core.Utils;
using Moongate.Server.Admin.Data.Config;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Syntax;

namespace Moongate.Server.Bootstrap.Internal.Setup;

/// <summary>Edits only certificate-related values, retaining unrelated TOML text and comments.</summary>
internal static class AdminApiConfigEditor
{
    public static string EnableGeneratedCertificate(string text)
    {
        DocumentSyntax document;
        TomlTable model;
        try
        {
            document = SyntaxParser.ParseStrict(text);
            model = TomlUtils.Deserialize<TomlTable>(text) ?? throw new InvalidDataException("Empty configuration.");
        }
        catch (TomlException)
        {
            throw new InvalidDataException("Cannot update admin_api: configuration is not valid TOML.");
        }
        var tables = document.Tables.OfType<TableSyntax>().Where(table => IsKey(table.Name, "admin_api")).ToArray();
        if (model.TryGetValue("admin_api", out var existing) && (existing is not TomlTable || tables.Length != 1))
        {
            throw new InvalidDataException("Certificate setup requires an explicit [admin_api] table; convert inline or dotted admin_api configuration first.");
        }
        var config = existing is TomlTable tableModel
            ? TomlUtils.Deserialize<AdminApiConfig>(TomlUtils.Serialize(tableModel))! : new AdminApiConfig();
        if (!string.IsNullOrEmpty(config.CertificatePath) && config.CertificatePath != "certificates/admin.pfx")
        {
            throw new InvalidOperationException("admin_api already references a custom certificate. Preserve it or explicitly clear certificate_path before generating a new identity.");
        }
        config.Enabled = true;
        config.AllowInsecureLoopback = false;
        config.CertificatePath = "certificates/admin.pfx";
        config.CertificatePassword = "";
        config.Validate();
        Dictionary<string, string> replacements = new()
        {
            ["enabled"] = "true", ["allow_insecure_loopback"] = "false",
            ["certificate_path"] = "\"certificates/admin.pfx\"", ["certificate_password"] = "\"\""
        };
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (tables.Length == 0)
        {
            return text + newline + "[admin_api]" + newline + string.Concat(replacements.Select(pair => $"{pair.Key} = {pair.Value}{newline}"));
        }
        var table = tables[0];
        List<(int Offset, int Length, string Value)> edits = [];
        foreach (var (key, value) in replacements)
        {
            var entry = table.Items.SingleOrDefault(item => IsKey(item.Key, key));
            if (entry is not null)
            {
                edits.Add((entry.Value!.Span.Offset, entry.Value.Span.Length, value));
            }
        }
        var missing = replacements.Where(pair => !table.Items.Any(item => IsKey(item.Key, pair.Key))).ToArray();
        if (missing.Length > 0)
        {
            var headerEnd = table.EndOfLineToken?.Span.End.Offset + 1 ?? table.CloseBracket!.Span.End.Offset + 1;
            var prefix = table.EndOfLineToken is null ? newline : "";
            edits.Add((headerEnd, 0, prefix + string.Concat(missing.Select(pair => $"{pair.Key} = {pair.Value}{newline}"))));
        }
        var result = new StringBuilder(text);
        foreach (var (offset, length, value) in edits.OrderByDescending(edit => edit.Offset))
        {
            result.Remove(offset, length).Insert(offset, value);
        }
        var updated = result.ToString();
        _ = TomlUtils.Deserialize<TomlTable>(updated);
        return updated;
    }

    private static bool IsKey(KeySyntax? key, string expected)
        => key is not null && !key.DotKeys.Any() && (key.Key switch
        {
            BareKeySyntax bare => bare.Key?.Text,
            StringValueSyntax quoted => quoted.Value,
            _ => null
        }) == expected;
}
