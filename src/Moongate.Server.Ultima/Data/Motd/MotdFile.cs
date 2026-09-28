namespace Moongate.Server.Ultima.Data.Motd;

/// <summary>Configuration read from data/motd.toml.</summary>
public sealed class MotdFile
{
    public List<string>? Lines { get; set; }
}
