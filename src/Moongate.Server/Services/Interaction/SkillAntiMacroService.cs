using Moongate.Server.Data.Interaction;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class SkillAntiMacroService : ISkillAntiMacroService
{
    private const int BucketSize = 8;
    private const int MaxAllowedRepeats = 3;
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(8);

    private readonly Func<DateTime> _utcNowProvider;
    private readonly Dictionary<SkillAntiMacroKey, SkillAntiMacroEntry> _entries = [];

    public SkillAntiMacroService()
        : this(static () => DateTime.UtcNow)
    {
    }

    internal SkillAntiMacroService(Func<DateTime> utcNowProvider)
    {
        ArgumentNullException.ThrowIfNull(utcNowProvider);
        _utcNowProvider = utcNowProvider;
    }

    public bool AllowGain(UOMobileEntity mobile, UOSkillName skillName, SkillGainContext? context)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (!mobile.IsPlayer || context is null)
        {
            return true;
        }

        var nowUtc = _utcNowProvider();
        var key = new SkillAntiMacroKey(
            mobile.Id,
            skillName,
            context.TargetId ?? Serial.Zero,
            context.Location.X / BucketSize,
            context.Location.Y / BucketSize
        );

        if (!_entries.TryGetValue(key, out var entry) || nowUtc - entry.LastAttemptAtUtc > RepeatWindow)
        {
            _entries[key] = new SkillAntiMacroEntry(1, nowUtc);
            return true;
        }

        var nextRepeatCount = entry.RepeatCount + 1;
        _entries[key] = new SkillAntiMacroEntry(nextRepeatCount, nowUtc);

        return nextRepeatCount <= MaxAllowedRepeats;
    }

    private readonly record struct SkillAntiMacroKey(
        Serial MobileId,
        UOSkillName SkillName,
        Serial TargetId,
        int BucketX,
        int BucketY
    );

    private readonly record struct SkillAntiMacroEntry(int RepeatCount, DateTime LastAttemptAtUtc);
}
