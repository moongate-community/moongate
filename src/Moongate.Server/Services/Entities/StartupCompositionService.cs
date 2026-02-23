using System.Text.Json;
using Moongate.Server.Data.Entities;
using Moongate.Server.Data.Startup;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Composes startup loadouts from layered startup template documents.
/// </summary>
public sealed class StartupCompositionService : IStartupCompositionService
{
    private readonly IPlaceholderResolverService _placeholderResolver;
    private readonly IStartupTemplateService _startupTemplateService;

    public StartupCompositionService(
        IStartupTemplateService startupTemplateService,
        IPlaceholderResolverService placeholderResolver
    )
    {
        _startupTemplateService = startupTemplateService;
        _placeholderResolver = placeholderResolver;
    }

    /// <inheritdoc />
    public StartupLoadout Compose(StarterProfileContext profileContext, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

        var loadout = new StartupLoadout();
        var profession = profileContext.Profession.Name;
        var race = profileContext.Race?.Name ?? "human";
        var gender = profileContext.Gender == GenderType.Female ? "female" : "male";

        ApplyBase(loadout, profileContext, playerName);
        ApplyRules(loadout, "startup_races", "race", race, profileContext, playerName);
        ApplyRules(loadout, "startup_genders", "gender", gender, profileContext, playerName);
        ApplyRules(loadout, "startup_professions", "profession", profession, profileContext, playerName);

        return loadout;
    }

    private void AddItems(
        List<StartupLoadoutItem> destination,
        JsonElement source,
        string propertyName,
        StarterProfileContext profileContext,
        string playerName
    )
    {
        if (!source.TryGetProperty(propertyName, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("templateId", out var templateIdElement) ||
                templateIdElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var templateId = templateIdElement.GetString();

            if (string.IsNullOrWhiteSpace(templateId))
            {
                continue;
            }

            var amount = item.TryGetProperty("amount", out var amountElement) &&
                         amountElement.TryGetInt32(out var parsedAmount)
                             ? Math.Max(1, parsedAmount)
                             : 1;

            JsonElement? args = null;

            if (item.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object)
            {
                args = _placeholderResolver.Resolve(argsElement, profileContext, playerName);
            }

            destination.Add(
                new()
                {
                    TemplateId = templateId,
                    Amount = amount,
                    Args = args
                }
            );
        }
    }

    private void ApplyBase(StartupLoadout loadout, StarterProfileContext profileContext, string playerName)
    {
        if (!_startupTemplateService.TryGet("startup_base", out var document) || document is null)
        {
            return;
        }

        AddItems(loadout.Backpack, document.Content, "backpack", profileContext, playerName);
        AddItems(loadout.Equip, document.Content, "equip", profileContext, playerName);
    }

    private void ApplyRules(
        StartupLoadout loadout,
        string templateId,
        string matchFieldName,
        string expectedValue,
        StarterProfileContext profileContext,
        string playerName
    )
    {
        if (!_startupTemplateService.TryGet(templateId, out var document) || document is null)
        {
            return;
        }

        if (!document.Content.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var rule in rules.EnumerateArray())
        {
            if (!RuleMatches(rule, matchFieldName, expectedValue))
            {
                continue;
            }

            if (rule.TryGetProperty("add", out var add))
            {
                AddItems(loadout.Backpack, add, "backpack", profileContext, playerName);
                AddItems(loadout.Equip, add, "equip", profileContext, playerName);
            }

            if (rule.TryGetProperty("remove", out var remove))
            {
                RemoveItems(loadout.Backpack, remove, "backpack");
                RemoveItems(loadout.Equip, remove, "equip");
            }
        }
    }

    private static void RemoveItems(List<StartupLoadoutItem> destination, JsonElement source, string propertyName)
    {
        if (!source.TryGetProperty(propertyName, out var removeArray) || removeArray.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var ids = removeArray
                  .EnumerateArray()
                  .Where(static item => item.ValueKind == JsonValueKind.String)
                  .Select(static item => item.GetString())
                  .Where(static id => !string.IsNullOrWhiteSpace(id))
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

        destination.RemoveAll(item => ids.Contains(item.TemplateId));
    }

    private static bool RuleMatches(JsonElement rule, string matchFieldName, string expectedValue)
    {
        if (!rule.TryGetProperty("match", out var match) || match.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!match.TryGetProperty(matchFieldName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return string.Equals(
            value.GetString(),
            expectedValue,
            StringComparison.OrdinalIgnoreCase
        );
    }
}
