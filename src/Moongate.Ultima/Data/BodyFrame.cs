namespace Moongate.Ultima.Data;

/// <summary>One decoded animation frame encoded as PNG, with its anchor point.</summary>
public sealed record BodyFrame
{
    public required Stream Png { get; init; }

    public int CenterX { get; init; }

    public int CenterY { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }
}
