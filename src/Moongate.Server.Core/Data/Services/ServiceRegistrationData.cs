namespace Moongate.Server.Core.Data.Services;

public record ServiceRegistrationData(Type ServiceType, Type ImplementationType, bool IsAutostart, int Priority = 0);
