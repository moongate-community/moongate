using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.UO.Data.Types;

namespace Moongate.Http.Plugin.Data.Api.Players;

/// <summary>A map-ready snapshot of one player currently in the world.</summary>
/// <param name="CharacterSerial">Stable character serial, formatted as hexadecimal.</param>
/// <param name="CharacterName">Character display name.</param>
/// <param name="AccountSerial">Owning account serial, formatted as hexadecimal.</param>
/// <param name="AccountUsername">Owning account username.</param>
/// <param name="MapId">Numeric UO facet id.</param>
/// <param name="MapName">Known facet name, or Unknown for an unrecognised id.</param>
/// <param name="X">World X coordinate.</param>
/// <param name="Y">World Y coordinate.</param>
/// <param name="Z">World altitude.</param>
/// <param name="Direction">Facing direction without the running flag.</param>
/// <param name="Running">Whether the latest direction carries the running flag.</param>
/// <param name="Body">Body graphic id.</param>
/// <param name="SkinHue">Skin hue id.</param>
/// <param name="Hits">Current hit points.</param>
/// <param name="HitsMax">Maximum hit points.</param>
/// <param name="Warmode">Whether the character is in war mode.</param>
public sealed record OnlinePlayerMapResponse(
    string CharacterSerial,
    string CharacterName,
    string AccountSerial,
    string AccountUsername,
    int MapId,
    string MapName,
    int X,
    int Y,
    int Z,
    string Direction,
    bool Running,
    int Body,
    int SkinHue,
    int Hits,
    int HitsMax,
    bool Warmode
)
{
    private const byte DirectionMask = 0x07;

    /// <summary>Copies session and character state into the public wire contract.</summary>
    public static OnlinePlayerMapResponse From(PlayerSession session, MobileEntity mobile)
    {
        var facing = (DirectionType)((byte)mobile.Direction & DirectionMask);
        var mapName = MapNameFor(mobile.MapId);

        return new(
            mobile.Id.ToString(),
            mobile.Name,
            session.AccountId.ToString(),
            session.Username ?? string.Empty,
            mobile.MapId,
            mapName,
            mobile.Position.X,
            mobile.Position.Y,
            mobile.Position.Z,
            facing.ToString(),
            (mobile.Direction & DirectionType.Running) != 0,
            mobile.Body,
            mobile.SkinHue.Value,
            mobile.Hits,
            mobile.HitsMax,
            mobile.Warmode
        );
    }

    private static string MapNameFor(int mapId)
    {
        if (mapId is < byte.MinValue or > byte.MaxValue)
        {
            return "Unknown";
        }

        var facet = (MapType)mapId;

        return Enum.IsDefined(facet) ? facet.ToString() : "Unknown";
    }
}
