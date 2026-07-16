namespace Moongate.Network.Types;

/// <summary>Why the server refused a lift request, as reported by reject move item request (0x27).</summary>
public enum LiftRejectReasonType : byte
{
    CannotLift = 0,
    OutOfRange = 1,
    OutOfSight = 2,
    TryToSteal = 3,
    AreHolding = 4,
    Inspecific = 5
}
