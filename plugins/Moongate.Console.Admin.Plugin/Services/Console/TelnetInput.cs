namespace Moongate.Console.Admin.Plugin.Services.Console;

/// <summary>
/// Cleans a decoded input line of control and replacement characters, then trims it. Telnet clients
/// send IAC negotiation bytes on connect; under UTF-8 those decode to the replacement char, so this
/// keeps them out of the first login line. Pure — no I/O. (Not a full telnet parser; <c>nc</c>/<c>socat</c>
/// are the recommended clients.)
/// </summary>
public static class TelnetInput
{
    public static string StripControls(string line)
        => new string(line.Where(c => !char.IsControl(c) && c != '�').ToArray()).Trim();
}
