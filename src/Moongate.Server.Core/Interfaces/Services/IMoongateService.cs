namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Marks a type as a service the Moongate host container registers and owns.</summary>
/// <remarks>
/// The registration extensions accept only types carrying this marker, so a service cannot be
/// registered by accident. A service that must run work at startup or shutdown implements
/// <see cref="IMoongateStartupService" /> instead of this interface alone.
/// </remarks>
public interface IMoongateService { }
