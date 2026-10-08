using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Training;

namespace Moongate.Server.Ultima.Data.Training;

/// <summary>
///     The session values of the skill trainers.
/// </summary>
public static class TrainingSessionKeys
{
    public static readonly SessionKey<TrainingQuote?> Quote = new("TrainingQuote");
}
