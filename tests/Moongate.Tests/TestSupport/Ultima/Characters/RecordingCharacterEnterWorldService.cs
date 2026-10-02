using Moongate.Core.Primitives;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Characters;

/// <summary>
///     Records the characters asked to enter the world and sends nothing.
/// </summary>
public sealed class RecordingCharacterEnterWorldService : ICharacterEnterWorldService
{
    public List<(Serial AccountId, CharacterForPlay Play)> Entered { get; } = [];

    public Task EnterAsync(PacketContext context, Serial accountId, CharacterForPlay play, CancellationToken cancellationToken)
    {
        Entered.Add((accountId, play));

        return Task.CompletedTask;
    }
}
