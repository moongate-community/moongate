using System.Globalization;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Loaders;
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
    ///     The seconds to wait after a skill when neither its script nor <c>skills.toml</c> gives any.
    /// </summary>
    public const double DefaultDelaySeconds = 1;

    /// <summary>
    ///     The least seconds to wait after a skill whose script is still running, having called <c>wait()</c>: what
    ///     it returns later is not read.
    /// </summary>
    public const double SuspendedDelaySeconds = 10;

    /// <summary>
    ///     "You may not use skills in jail."
    /// </summary>
    public const int NoSkillsInJailMessage = 30168;

    /// <summary>
    ///     The seconds between two "You must wait" told to one character.
    /// </summary>
    public const double MustWaitMessageSeconds = 1;


    private readonly IMobileService _mobiles;
    private readonly ISkillScriptService _scripts;
    private readonly ISpeechService _speech;
    private readonly TimeProvider _time;
    private readonly Lazy<Dictionary<SkillType, SkillContent>> _data;
    private readonly IJailService? _jail;
    private readonly ILocalizationService? _localization;

    public SkillUseService(
        IMobileService mobiles,
        ISkillScriptService scripts,
        ISpeechService speech,
        TimeProvider time,
        IDataLoaderService data,
        IJailService? jail = null,
        ILocalizationService? localization = null
    )
    {
        _data = new(() => data.GetEntities<SkillContent>().ToDictionary(content => content.Id));
        _jail = jail;
        _localization = localization;
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

        // As ModernUO's jail region: a prisoner uses no skill. The staff is never held to it.
        if (session.AccountType < AccountType.GameMaster && _jail?.GetSentence(user.Id) is not null)
        {
            _speech.Tell(user, _localization.Text(NoSkillsInJailMessage, "You may not use skills in jail."));

            return false;
        }

        var now = _time.GetUtcNow();

        if (user.NextSkillAt is { } next && now < next)
        {
            // As ModernUO, a macro in a loop is not answered at every try.
            if (user.NextSkillMessageAt is not { } quiet || now >= quiet)
            {
                user.NextSkillMessageAt = now.AddSeconds(MustWaitMessageSeconds);
                _speech.TellCliloc(user, MustWaitCliloc);
            }

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

        // What the script returned, else what skills.toml says of the skill, else a second.
        var ofTheSkill = _data.Value.TryGetValue(skill, out var content) ? content.Delay : null;
        user.NextSkillAt = now.AddSeconds(
            result.Kind == ScriptResultKind.Suspended
                ? Math.Max(SuspendedDelaySeconds, ofTheSkill ?? 0)
                : DelayOf(result.Values) ?? ofTheSkill ?? DefaultDelaySeconds
        );

        return true;
    }

    // The seconds the script returned, from none to an hour; null when it returned nothing that is a number.
    private static double? DelayOf(IReadOnlyList<object?> values)
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

        return double.IsNaN(seconds) ? null : Math.Clamp(seconds, 0, SkillsLoader.MaximumDelaySeconds);
    }
}
