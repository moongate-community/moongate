namespace Moongate.Server.Ultima.Types.Weather;

/// <summary>
///     The weather the client shows, with the values of packet 0x65.
/// </summary>
public enum WeatherKindType : byte
{
    Rain = 0,
    Storm = 1,
    Snow = 2,
    None = 0xFF
}
