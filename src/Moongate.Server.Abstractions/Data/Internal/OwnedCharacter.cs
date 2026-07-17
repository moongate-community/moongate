using Moongate.Persistence.Entities;

namespace Moongate.Server.Abstractions.Data.Internal;

/// <summary>A character paired with the account that owns it, on the way to the API.</summary>
public sealed record OwnedCharacter(MobileEntity Mobile, string AccountUsername);
