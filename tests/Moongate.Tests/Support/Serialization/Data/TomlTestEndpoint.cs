namespace Moongate.Tests.Support.Serialization.Data;

public sealed class TomlTestEndpoint
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 2593;
}
