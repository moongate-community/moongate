namespace Moongate.Tests.TestSupport.Environment;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentTestsCollection
{
    public const string Name = "Environment mutation";
}
