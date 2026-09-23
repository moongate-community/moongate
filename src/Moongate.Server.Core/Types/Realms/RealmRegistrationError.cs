namespace Moongate.Server.Core.Types.Realms;

/// <summary>Stable registration rejection codes returned over the internal API.</summary>
public enum RealmRegistrationError : byte
{
    None = 0,
    IdentityMismatch = 1,
    InvalidDescriptor = 2,
    DuplicateIndex = 3,
    StaleLease = 4,
    CapacityExceeded = 5,
    ExpiredLease = 6
}
