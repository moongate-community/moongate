namespace Moongate.Core.Server.Data.Internal.Services;

public record struct ServiceDefinitionObject(Type ServiceType, Type ImplementationType, int Priority = 0);
