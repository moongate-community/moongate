using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.UO.Data.Data.Reputation;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Data.Internal.Interaction;

internal static class CharacterProfileHelper
{
    public static string BuildFooter(GameSession session, UOMobileEntity requester, UOMobileEntity target, UOAccountEntity? account)
    {
        var isSelf = requester.Id == target.Id;

        if (IsLocked(target))
        {
            if (isSelf)
            {
                return "Your profile has been locked.";
            }

            return session.AccountType >= AccountType.GameMaster ? "This profile has been locked." : string.Empty;
        }

        if (!isSelf || account is null)
        {
            return string.Empty;
        }

        return FormatAccountAge(DateTime.UtcNow - account.CreatedUtc);
    }

    public static string BuildHeader(UOMobileEntity requester, UOMobileEntity target)
    {
        var configuration = ReputationTitleRuntime.Current;
        var parts = new List<string>(4);

        if (ShouldIncludeReputationTitle(requester, target))
        {
            var reputationTitle = GetReputationTitle(target.Fame, target.Karma, configuration);

            if (!string.IsNullOrWhiteSpace(reputationTitle))
            {
                parts.Add(reputationTitle);
            }
        }

        if (target.Fame >= 10000)
        {
            parts.Add(target.Gender == GenderType.Female ? configuration.Honorifics.Female : configuration.Honorifics.Male);
        }

        if (!string.IsNullOrWhiteSpace(target.Name))
        {
            parts.Add(target.Name);
        }

        if (!string.IsNullOrWhiteSpace(target.Title))
        {
            parts.Add(target.Title);
        }

        return string.Join(' ', parts);
    }

    public static string GetBody(UOMobileEntity mobile)
        => mobile.TryGetCustomString(MobileCustomParamKeys.Profile.Body, out var body) && !string.IsNullOrEmpty(body)
            ? body
            : string.Empty;

    public static Serial GetDisplaySerial(UOMobileEntity requester, UOMobileEntity target)
        => requester.Id == target.Id && IsLocked(target) ? Serial.Zero : target.Id;

    public static bool IsLocked(UOMobileEntity mobile)
        => mobile.TryGetCustomBoolean(MobileCustomParamKeys.Profile.Locked, out var locked) && locked;

    public static string SanitizeBody(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var buffer = new char[normalized.Length];

        for (var i = 0; i < normalized.Length; i++)
        {
            var chr = normalized[i];
            buffer[i] = chr == '\n' || chr == '\t' || !char.IsControl(chr) ? chr : ' ';
        }

        return new string(buffer);
    }

    public static void SetBody(UOMobileEntity mobile, string? body)
        => mobile.SetCustomString(MobileCustomParamKeys.Profile.Body, SanitizeBody(body));

    public static void SetLocked(UOMobileEntity mobile, bool locked)
        => mobile.SetCustomBoolean(MobileCustomParamKeys.Profile.Locked, locked);

    private static string FormatAccountAge(TimeSpan age)
    {
        if (TryFormatAge(age.TotalDays, "This account is {0} day{1} old.", out var formatted))
        {
            return formatted;
        }

        if (TryFormatAge(age.TotalHours, "This account is {0} hour{1} old.", out formatted))
        {
            return formatted;
        }

        if (TryFormatAge(age.TotalMinutes, "This account is {0} minute{1} old.", out formatted))
        {
            return formatted;
        }

        return TryFormatAge(age.TotalSeconds, "This account is {0} second{1} old.", out formatted)
            ? formatted
            : string.Empty;
    }

    private static string GetReputationTitle(
        int fame,
        int karma,
        ReputationTitleConfiguration configuration
    )
    {
        var fameEntry = configuration.FameBuckets[^1];

        foreach (var candidate in configuration.FameBuckets)
        {
            fameEntry = candidate;

            if (fame <= candidate.MaxFame)
            {
                break;
            }
        }

        var karmaEntry = fameEntry.KarmaBuckets[^1];

        foreach (var candidate in fameEntry.KarmaBuckets)
        {
            karmaEntry = candidate;

            if (karma <= candidate.MaxKarma)
            {
                break;
            }
        }

        return karmaEntry.Title;
    }

    private static bool ShouldIncludeReputationTitle(UOMobileEntity requester, UOMobileEntity target)
        => requester.Id == target.Id || target.Fame >= 5000;

    private static bool TryFormatAge(double value, string format, out string result)
    {
        if (value >= 1.0)
        {
            var amount = (int)value;
            result = string.Format(format, amount, amount != 1 ? "s" : "");

            return true;
        }

        result = string.Empty;

        return false;
    }
}
