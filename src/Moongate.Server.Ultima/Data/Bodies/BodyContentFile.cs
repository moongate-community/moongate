namespace Moongate.Server.Ultima.Data.Bodies;

/// <summary>
///     The root of <c>bodies.toml</c>: one list of body ids or <c>"min-max"</c> ranges per kind of body.
/// </summary>
internal sealed class BodyContentFile
{
    public List<string> Human { get; set; } = [];

    public List<string> Animal { get; set; } = [];

    public List<string> Monster { get; set; } = [];

    public List<string> Sea { get; set; } = [];

    public List<string> Equipment { get; set; } = [];
}
