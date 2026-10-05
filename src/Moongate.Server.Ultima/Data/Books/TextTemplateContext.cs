namespace Moongate.Server.Ultima.Data.Books;

public sealed record TextTemplateContext
{
    public string ServerName { get; init; } = "";
    public string RealmName { get; init; } = "";
    public string Version { get; init; } = "";
    public string Codename { get; init; } = "";
    public string PlayerName { get; init; } = "";
    public int UsersOnline { get; init; }
}

