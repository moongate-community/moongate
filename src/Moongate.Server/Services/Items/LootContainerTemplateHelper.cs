using System.Globalization;
using Moongate.UO.Data.Templates.Items;

namespace Moongate.Server.Services.Items;

internal static class LootContainerTemplateHelper
{
    private const string LootRefillableParamKey = "loot_refillable";
    private const string LootRefillSecondsParamKey = "loot_refill_seconds";

    public static TimeSpan? GetRefillDelay(ItemTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!IsRefillable(template) ||
            !template.Params.TryGetValue(LootRefillSecondsParamKey, out var param) ||
            string.IsNullOrWhiteSpace(param.Value))
        {
            return null;
        }

        if (!int.TryParse(param.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            seconds <= 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    public static bool IsRefillable(ItemTemplateDefinition template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!template.Params.TryGetValue(LootRefillableParamKey, out var param) ||
            string.IsNullOrWhiteSpace(param.Value))
        {
            return false;
        }

        return bool.TryParse(param.Value, out var refillable) && refillable;
    }
}
