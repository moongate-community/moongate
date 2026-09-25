namespace Moongate.Server.Ultima.Data.Containers;

/// <summary>
///     The root of <c>containers.toml</c>: an array of tables under <c>container</c>. The property name must match
///     the table name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class ContainerContentFile
{
    public List<ContainerContent> Container { get; set; } = [];
}
