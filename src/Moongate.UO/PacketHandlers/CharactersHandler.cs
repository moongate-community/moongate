using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Session;
using Moongate.UO.Interfaces.Handlers;

namespace Moongate.UO.PacketHandlers;

public class CharactersHandler : IGamePacketHandler
{
    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
    {
        if (packet is CharacterCreationPacket characterCreation)
        {
            await CreateCharacterAsync(session, characterCreation);
        }
    }

    private async Task CreateCharacterAsync(
        GameSession session, CharacterCreationPacket characterCreation
    )
    {
        // Validate character creation data
        // Create the character in the database
        // Send confirmation packet back to the client
    }
}
