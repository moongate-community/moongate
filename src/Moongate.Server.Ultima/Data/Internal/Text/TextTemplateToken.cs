namespace Moongate.Server.Ultima.Data.Internal.Text;

public sealed record TextTemplateToken
{
    public int Index { get; init; }
    public int Length { get; init; }
    public string? Name { get; init; }
}
