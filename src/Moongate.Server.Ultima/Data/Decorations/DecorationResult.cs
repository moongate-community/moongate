namespace Moongate.Server.Ultima.Data.Decorations;

/// <summary>
///     What a whole <c>.decorate</c> did: items placed, items already there, placements skipped, files read.
/// </summary>
public sealed record DecorationResult(int Placed, int Present, int Skipped, int Files);
