using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Internal.Training;

/// <summary>
///     The price a trainer quoted a player, until it is paid, replaced or the session ends.
/// </summary>
/// <param name="Trainer">The serial of the trainer.</param>
/// <param name="Skill">The skill quoted.</param>
public sealed record TrainingQuote(Serial Trainer, SkillType Skill);
