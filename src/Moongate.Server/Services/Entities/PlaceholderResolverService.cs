using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Moongate.Server.Data.Entities;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Replaces known startup placeholder tokens in JSON args payloads.
/// </summary>
public sealed partial class PlaceholderResolverService : IPlaceholderResolverService
{
    private static readonly Regex PlaceholderRegex = PlaceHolderGeneratedRegex();

    /// <inheritdoc />
    public JsonElement Resolve(JsonElement args, StarterProfileContext profileContext, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var node = JsonNode.Parse(args.GetRawText())!;
        var tokens = CreateTokens(profileContext, playerName);
        ReplaceTokens(node, tokens);

        return JsonSerializer.SerializeToElement(node);
    }

    private static Dictionary<string, string> CreateTokens(StarterProfileContext profileContext, string playerName)
    {
        var gender = profileContext.Gender == GenderType.Female ? "female" : "male";
        var raceName = profileContext.Race?.Name ?? "human";
        var professionName = profileContext.Profession.Name;

        return new(StringComparer.Ordinal)
        {
            ["<playerName>"] = playerName,
            ["<raceName>"] = raceName,
            ["<professionName>"] = professionName,
            ["<gender>"] = gender
        };
    }

    [GeneratedRegex("<[A-Za-z][A-Za-z0-9]*>", RegexOptions.Compiled)]
    private static partial Regex PlaceHolderGeneratedRegex();

    private static void ReplaceTokens(JsonNode? node, IReadOnlyDictionary<string, string> tokens)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var property in jsonObject.ToList())
            {
                ReplaceTokens(property.Value, tokens);
            }

            return;
        }

        if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                ReplaceTokens(jsonArray[index], tokens);
            }

            return;
        }

        if (node is not JsonValue textValue || !textValue.TryGetValue<string>(out var text))
        {
            return;
        }

        var replaced = PlaceholderRegex.Replace(
            text,
            match => tokens.TryGetValue(match.Value, out var value) ? value : match.Value
        );

        if (!string.Equals(replaced, text, StringComparison.Ordinal))
        {
            node.ReplaceWith(JsonValue.Create(replaced));
        }
    }
}
