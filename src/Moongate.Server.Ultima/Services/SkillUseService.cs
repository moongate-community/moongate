using System.Globalization;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Runs the script of the skill a player uses and keeps the wait before the next one, as ModernUO does: the wait
///     is of the character, one for all its skills, and each skill says how long it is.
/// </summary>
public sealed class SkillUseService : ISkillUseService
{
    public const int MustWaitCliloc = 500118;
    public const int CannotUseCliloc = 500014;

    /// <summary>
    ///     The seconds to wait after a skill whose script returns none, or fails.
    /// </summary>
    public const double DefaultDelaySeconds = 1;

    private const double MaximumDelaySeconds = 3600;

    private readonly IMobileService _mobiles;
    private readonly ISkillScriptService _scripts;
    private readonly ISpeechService _speech;
    private readonly TimeProvider _time;

    public SkillUseService(IMobileService mobiles, ISkillScriptService scripts, ISpeechService speech, TimeProvider time)
    {
        _mobiles = mobiles;
        _scripts = scripts;
        _speech = speech;
        _time = time;
    }

    public bool Use(GameSession session, SkillType skill)
    {
        if (!session.CharacterId.IsValid ||
            !_mobiles.TryGet(session.CharacterId, out var user) ||
            !_mobiles.IsInWorld(user.Id))
        {
            return false;
        }

        var now = _time.GetUtcNow();

        if (user.NextSkillAt is { } next && now < next)
        {
            _speech.TellCliloc(user, MustWaitCliloc);

            return false;
        }

        // Taken before the script runs: a script that waits, or uses a skill itself, cannot be run twice at once.
        user.NextSkillAt = now.AddSeconds(DefaultDelaySeconds);
        var result = _scripts.Use(skill, user);

        if (result.Kind == ScriptResultKind.Missing)
        {
            user.NextSkillAt = null;
            _speech.TellCliloc(user, CannotUseCliloc);

            return false;
        }

        user.NextSkillAt = now.AddSeconds(DelayOf(result.Values));

        return true;
    }

    // The seconds the script returned, from none to an hour; one when it returned nothing that is a number.
    private static double DelayOf(IReadOnlyList<object?> values)
    {
        var seconds = values.Count > 0
                          ? values[0] switch
                          {
                              double value => value,
                              long value   => value,
                              int value    => value,
                              string value when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                              _            => double.NaN
                          }
                          : double.NaN;

        return double.IsNaN(seconds) ? DefaultDelaySeconds : Math.Clamp(seconds, 0, MaximumDelaySeconds);
    }
}
