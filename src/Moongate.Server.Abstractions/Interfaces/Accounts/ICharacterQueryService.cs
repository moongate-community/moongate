using Moongate.Server.Abstractions.Data.Internal;
using SquidStd.Persistence.Abstractions.Data;

namespace Moongate.Server.Abstractions.Interfaces.Accounts;

/// <summary>Read-only, paged views over every player character. Exists for the admin API.</summary>
public interface ICharacterQueryService
{
    /// <summary>
    /// One page of player characters with their owners, ordered by character name, filtered by a free-text
    /// match on the character's name or the owning account's username. NPCs are excluded.
    /// </summary>
    /// <param name="search">Case-insensitive substring, or null for everything.</param>
    /// <param name="skip">Characters to skip. Past the end yields an empty page and the true total.</param>
    /// <param name="take">Page size.</param>
    PagedResult<OwnedCharacter> Search(string? search, int skip, int take);
}
