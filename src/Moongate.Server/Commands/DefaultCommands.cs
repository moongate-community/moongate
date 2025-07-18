using DryIoc;
using Moongate.Core.Extensions.Collections;
using Moongate.Core.Server.Data.Internal.Commands;
using Moongate.Core.Server.Instances;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Services;
using Moongate.UO.Data.Packets.Effects;
using Moongate.UO.Data.Packets.Items;
using Moongate.UO.Data.Packets.Mouse;
using Moongate.UO.Data.Packets.World;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Services;
using Moongate.UO.Interfaces.Services.Systems;

namespace Moongate.Server.Commands;

public static class DefaultCommands
{
    public static void RegisterDefaultCommands(ICommandSystemService commandSystemService)
    {
        commandSystemService.RegisterCommand(
            "save",
            OnSaveCommand,
            "Requests a save of the current game state.",
            AccountLevelType.Admin,
            CommandSourceType.Console
        );

        commandSystemService.RegisterCommand(
            "where",
            OnWhereCommand,
            "Returns the current location",
            AccountLevelType.User,
            CommandSourceType.InGame
        );

        commandSystemService.RegisterCommand(
            "add_item",
            OnAddItemCommand,
            "Adds an item to your backpack",
            AccountLevelType.User,
            CommandSourceType.InGame
        );

        commandSystemService.RegisterCommand(
            "set_weather",
            OnSetWeatherCommand,
            "Sets the weather in the current map",
            AccountLevelType.Admin,
            CommandSourceType.InGame
        );

        commandSystemService.RegisterCommand(
            "broadcast",
            OnBroadcastCommand,
            "Broadcasts a message to all players",
            AccountLevelType.Admin
        );

        commandSystemService.RegisterCommand("color", OnColorCommand, "Changes the color of an item", AccountLevelType.User);

        commandSystemService.RegisterCommand("select", OnSelect, "Selects an item", AccountLevelType.User);

        commandSystemService.RegisterCommand(
            "blood",
            OnBloodCommand,
            "Spawns a blood effect at your location",
            AccountLevelType.User
        );

        commandSystemService.RegisterCommand("music", OnMusicCommand, "Plays a music track", AccountLevelType.User);

        commandSystemService.RegisterCommand("orion", OnOrionCommand, "Add my cat in world", AccountLevelType.User);
    }

    private static async Task OnOrionCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);
        var entityFactoryService = MoongateContext.Container.Resolve<IEntityFactoryService>();

        var mobileService = MoongateContext.Container.Resolve<IMobileService>();

        var mobile = entityFactoryService.CreateMobileEntity("orione");

        mobile.Map = gameSession.Mobile.Map;

        mobile.Location = gameSession.Mobile.Location + new Point3D(1, 1, 0);

        mobileService.AddInWorld(mobile);
    }

    private static Task OnMusicCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);

        if (gameSession?.Mobile == null)
        {
            return Task.CompletedTask;
        }

        var randomEnumValue = Enum.GetValues<MusicName>().ToList().RandomElement();
        var musicPacket = new PlayMusicPacket((int)randomEnumValue);
        gameSession.SendPackets(musicPacket);
        context.Print("Playing music: {0}", randomEnumValue);

        return Task.CompletedTask;
    }

    private static Task OnBloodCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);

        var mobile = gameSession.Mobile;

        if (mobile == null)
        {
            return Task.CompletedTask;
        }

        var bloodPacket = new BloodPacket(mobile);
        gameSession.SendPackets(bloodPacket);

        return Task.CompletedTask;
    }

    private static async Task OnSelect(CommandSystemContext context)
    {
        var cursorPacket = new TargetCursorPacket(CursorSelectionType.Object, CursorType.Helpful, Serial.Parse("1234"));
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);

        gameSession.SendPackets(cursorPacket);
    }

    private static async Task OnColorCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);

        gameSession.SendPackets(new DyeWindowRequestPacket(Serial.Zero));
    }

    private static async Task OnBroadcastCommand(CommandSystemContext context)
    {
        if (context.Arguments.Length == 0)
        {
            context.Print("Usage: broadcast <message>");
            return;
        }

        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();


        var text = string.Join(" ", context.Arguments);

        foreach (var session in gameSessionService.GetSessions())
        {
            session.Mobile.ReceiveSpeech(null, ChatMessageType.System, 0, $"[SYSTEM] {text}", 0, 3);
        }
    }

    private static Task OnSetWeatherCommand(CommandSystemContext context)
    {
        var setWeatherPacket = new SetWeatherPacket(WeatherType.Snow, 70, 10);

        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);
        gameSession.SendPackets(setWeatherPacket);

        return Task.CompletedTask;
    }

    private static Task OnAddItemCommand(CommandSystemContext context)
    {
        var itemTemplateName = context.Arguments[0];
        var factoryService = MoongateContext.Container.Resolve<IEntityFactoryService>();

        var item = factoryService.CreateItemEntity(itemTemplateName);

        if (item == null)
        {
            context.Print("Item template '{0}' not found.", itemTemplateName);
            return Task.CompletedTask;
        }

        context.Print("Adding item '{0}'...", item);

        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();
        var gameSession = gameSessionService.GetSession(context.SessionId);

        gameSession.Mobile.GetBackpack().AddItem(item, Point2D.Zero);

        return Task.CompletedTask;
    }

    private static async Task OnWhereCommand(CommandSystemContext context)
    {
        var gameSessionService = MoongateContext.Container.Resolve<IGameSessionService>();

        var mobile = gameSessionService.GetSession(context.SessionId).Mobile;
        context.Print(
            "You are at Map {0} (X: {1}, Y: {2}, Z: {3})",
            mobile.Map.MapID,
            mobile.Location.X,
            mobile.Location.Y,
            mobile.Location.Z
        );
    }

    private static async Task OnSaveCommand(CommandSystemContext context)
    {
        var persistenceService = MoongateContext.Container.Resolve<IPersistenceService>();

        persistenceService.RequestSave();
    }
}
