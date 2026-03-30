using System.Globalization;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Normalizes persisted paralyze state and shared movement-lock checks.
/// </summary>
public static class ParalyzeStateHelper
{
    public const string ExpiresAtUtcKey = "magic.paralyze_until_utc";
    public const string TimerIdKey = "magic.paralyze_timer_id";
    private const string TimerNamePrefix = "spell_paralyze_";

    public static void Apply(UOMobileEntity mobile, DateTime expiresAtUtc, string timerId)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        ArgumentException.ThrowIfNullOrWhiteSpace(timerId);

        mobile.IsParalyzed = true;
        mobile.SetCustomString(ExpiresAtUtcKey, expiresAtUtc.ToString("O", CultureInfo.InvariantCulture));
        mobile.SetCustomString(TimerIdKey, timerId);
    }

    public static bool BlocksMovement(UOMobileEntity mobile, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.IsFrozen)
        {
            return true;
        }

        return HasActiveParalyze(mobile, nowUtc);
    }

    public static void Clear(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        mobile.IsParalyzed = false;
        mobile.RemoveCustomProperty(ExpiresAtUtcKey);
        mobile.RemoveCustomProperty(TimerIdKey);
    }

    public static string GetTimerName(Serial mobileId)
        => $"{TimerNamePrefix}{mobileId}";

    public static bool TryGetTimerId(UOMobileEntity mobile, out string? timerId)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        timerId = null;

        return mobile.TryGetCustomString(TimerIdKey, out timerId) && !string.IsNullOrWhiteSpace(timerId);
    }

    public static bool HasActiveParalyze(UOMobileEntity mobile, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!mobile.IsParalyzed)
        {
            return false;
        }

        if (!TryGetExpiresAtUtc(mobile, out var expiresAtUtc))
        {
            return true;
        }

        if (expiresAtUtc <= nowUtc)
        {
            Clear(mobile);

            return false;
        }

        return true;
    }

    public static bool TryGetExpiresAtUtc(UOMobileEntity mobile, out DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        expiresAtUtc = default;

        if (!mobile.TryGetCustomString(ExpiresAtUtcKey, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        return DateTime.TryParse(
            rawValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out expiresAtUtc
        );
    }
}
