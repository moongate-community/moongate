namespace Moongate.UO.Data.Data.Reputation;

/// <summary>
/// Maps an upper karma bound to a reputation title prefix.
/// </summary>
public sealed record ReputationKarmaBucket(int MaxKarma, string Title);
