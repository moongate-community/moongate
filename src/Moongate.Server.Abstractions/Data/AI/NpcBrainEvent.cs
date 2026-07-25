using Moongate.Core.Geometry;
using Moongate.Server.Abstractions.Types;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Abstractions.Data.AI;

public sealed record NpcBrainEvent(
    NpcBrainEventType Type,
    BrainMobileSnapshot? Mobile = null,
    string? Text = null,
    ChatMessageType? SpeechType = null,
    Point3D? FromPosition = null,
    Point3D? ToPosition = null,
    int Amount = 0
);
