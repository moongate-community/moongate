namespace Moongate.Tests.Support.Serialization.Data;

public sealed class TomlTestSettings
{
    public string ServerName { get; set; } = "Moongate";

    public bool Enabled { get; set; } = true;

    public string[] Tags { get; set; } = [];

    public TomlTestEndpoint Network { get; set; } = new();
}
