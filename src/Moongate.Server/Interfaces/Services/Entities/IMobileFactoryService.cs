using Moongate.Network.Packets.Incoming.Login;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Interfaces.Services.Entities;

/// <summary>
/// Creates mobile entities from templates and character creation packets.
/// </summary>
public interface IMobileFactoryService
{
    /// <summary>
    /// Creates a mobile entity from a mobile template id.
    /// </summary>
    /// <param name="mobileTemplateId">Mobile template identifier.</param>
    /// <param name="accountId">Optional owner account identifier for player mobiles.</param>
    /// <returns>Initialized mobile entity with allocated serial.</returns>
    UOMobileEntity CreateMobileFromTemplate(string mobileTemplateId, Serial? accountId = null);

    /// Creates a player mobile from character creation packet data.
    /// </summary>
    /// <param name="packet">Character creation packet.</param>
    /// <param name="accountId">Owner account serial identifier.</param>
    /// <returns>Initialized mobile entity with allocated serial.</returns>
    UOMobileEntity CreatePlayerMobile(CharacterCreationPacket packet, Serial accountId);
}
