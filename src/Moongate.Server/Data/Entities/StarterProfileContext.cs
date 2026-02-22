using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Entities;

/// <summary>
/// Starter profile information used to resolve initial character equipment and inventory.
/// </summary>
public readonly record struct StarterProfileContext(ProfessionInfo Profession, Race? Race, GenderType Gender);
