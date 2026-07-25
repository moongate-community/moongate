namespace Moongate.Tests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalSerilogCollection
{
    public const string Name = "GlobalSerilog";
}
