using DryIoc;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the commands of the game role, for the console and for game masters and administrators in game.
/// </summary>
public static class UltimaCommandsContainerExtensions
{
    /// <summary>
    ///     Registers the commands of the game role, for the console and for game masters and administrators in game.
    /// </summary>
    public static Container AddUltimaCommands(this Container container)
    {
        container.RegisterCommand<ShutdownCommand>(
            "shutdown",
            "Shuts down this server gracefully, immediately or after a delay: shutdown [seconds].",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator,
            CommandMessages.ShutdownDescription
        );
        container.RegisterCommand<SaveCommand>(
            "save",
            "Saves the world and tells every player when it is done.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator,
            CommandMessages.SaveDescription
        );
        container.RegisterCommand<BroadcastCommand>(
            "broadcast",
            "Sends a system message to every player in this world: broadcast <text>.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator,
            CommandMessages.BroadcastDescription
        );
        container.RegisterCommand<SpawnCommand>(
            "spawn",
            "Spawns an NPC from a mobile template where you target: spawn <template>.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.SpawnDescription
        );
        container.RegisterCommand<AddCommand>(
            "add",
            "Puts an item from an item template on the ground where you target: add <template>.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.AddDescription
        );
        container.RegisterCommand<RemoveCommand>(
            "remove",
            "Removes the NPC or the item on the ground you target.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.RemoveDescription
        );
        container.RegisterCommand<FameCommand>(
            "fame",
            "Sets the fame (0 to 32000) of the character or NPC you target.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.FameDescription
        );
        container.RegisterCommand<KarmaCommand>(
            "karma",
            "Sets the karma (-32000 to 32000) of the character or NPC you target.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.KarmaDescription
        );
        container.RegisterCommand<DecorateCommand>(
            "decorate",
            "Places the world decoration: doors, signs, lights and furniture.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator,
            CommandMessages.DecorateDescription
        );
        container.RegisterCommand<GlobalLightCommand>(
            "globallight",
            "Sets the light of every player (0 brightest, 31 darkest) or, without a level, goes back to the time of day.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.GlobalLightDescription
        );
        container.RegisterCommand<WeatherCommand>(
            "weather",
            "Shows the weather where you stand or, with none, rain, snow or storm, forces it there until the next game hour.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.WeatherDescription
        );
        container.RegisterCommand<GumpCommand>(
            "gump",
            "Opens a gump of templates/gumps on you to try it; name=value pairs fill its placeholders.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.GumpDescription
        );
        container.RegisterCommand<InitialSpawnCommand>(
            "initial_spawn",
            "Fills every spawn region to its max at the next spawn check.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.Administrator,
            CommandMessages.InitialSpawnDescription
        );
        container.RegisterCommand<SpawnsCommand>(
            "spawns",
            "Lists the spawn regions where you stand, with their live NPCs and the minutes to their next spawn.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.SpawnsDescription
        );
        container.RegisterCommand<TimeCommand>(
            "time",
            "Shows the game time and the moons where you stand.",
            CommandSourceType.InGame,
            AccountType.Regular,
            CommandMessages.TimeDescription
        );
        container.RegisterCommand<SeasonCommand>(
            "season",
            "Shows the season where you stand and your map's or, with a season or auto, sets your map's until the restart.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.SeasonDescription
        );
        container.RegisterCommand<MusicCommand>(
            "music",
            "Shows the music where you stand or, with a track name, plays it to you until your next region change.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.MusicDescription
        );
        container.RegisterCommand<LockCommand>(
            "lock",
            "Locks the door you target and the door linked to it: players cannot open it.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.LockDescription
        );
        container.RegisterCommand<UnlockCommand>(
            "unlock",
            "Unlocks the door you target and the door linked to it.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.UnlockDescription
        );
        container.RegisterCommand<KeyCommand>(
            "key",
            "Puts in your backpack a key for the door you target and its linked door.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.KeyDescription
        );
        container.RegisterCommand<GoCommand>(
            "go",
            "Takes you to a place: go alone lists the named ones, go <place> goes to one, go <x>,<y>,<z> [map] to a spot.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.GoDescription
        );
        container.RegisterCommand<MoongateCommand>(
            "moongate",
            "Puts at your feet a moongate to a place of your map or of another: moongate <x>,<y>,<z> [map].",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.MoongateDescription
        );
        container.RegisterCommand<WhereCommand>(
            "where",
            "Shows what you target: its serial, or the map and location of a spot.",
            CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.WhereDescription
        );
        container.RegisterCommand<CharacterCommand>(
            "character",
            "Pending character deletions: character pending [account-serial]; character restore <character-serial>.",
            CommandSourceType.Console | CommandSourceType.InGame,
            AccountType.GameMaster,
            CommandMessages.CharacterDescription
        );

        return container;
    }
}
