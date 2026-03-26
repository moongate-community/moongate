using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Modules;

[ScriptModule("guards", "Provides thin guard primitives for Lua brains.")]

/// <summary>
/// Exposes guard-specific runtime helpers to Lua scripts.
/// </summary>
public sealed class GuardsModule
{
    private const string GuardFocusSerialKey = "guard_focus_serial";
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly MobileMovementModule _movementModule;

    public GuardsModule(
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameNetworkSessionService,
        IMovementValidationService? movementValidationService = null,
        IPathfindingService? pathfindingService = null,
        IGameEventBusService? gameEventBusService = null,
        IBackgroundJobService? backgroundJobService = null,
        ISpeechService? speechService = null
    )
    {
        _spatialWorldService = spatialWorldService;
        _movementModule = new(
            speechService ?? NullSpeechService.Instance,
            gameNetworkSessionService,
            spatialWorldService,
            movementValidationService,
            pathfindingService,
            gameEventBusService,
            backgroundJobService
        );
    }

    [ScriptFunction("set_focus", "Sets or clears the guard focus target serial.")]
    public bool SetFocus(uint guardSerial, uint? targetSerial = null)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, guardSerial, out var resolvedGuard))
        {
            return false;
        }

        var guard = resolvedGuard!;

        if (targetSerial is null)
        {
            if (!guard.CustomProperties.ContainsKey(GuardFocusSerialKey))
            {
                return true;
            }

            return guard.RemoveCustomProperty(GuardFocusSerialKey);
        }

        if (targetSerial == 0)
        {
            return false;
        }

        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial.Value, out var resolvedTarget))
        {
            return false;
        }

        guard.SetCustomInteger(GuardFocusSerialKey, (long)resolvedTarget!.Id);

        return true;
    }

    [ScriptFunction("get_focus", "Gets the guard focus target serial, or nil when unavailable.")]
    public uint? GetFocus(uint guardSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, guardSerial, out var resolvedGuard))
        {
            return null;
        }

        var guard = resolvedGuard!;

        if (!guard.TryGetCustomInteger(GuardFocusSerialKey, out var focusSerial) ||
            focusSerial <= 0 ||
            focusSerial > uint.MaxValue ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, (uint)focusSerial, out _))
        {
            return null;
        }

        return (uint)focusSerial;
    }

    [ScriptFunction("teleport_to_target", "Teleports the guard to the target mobile location.")]
    public bool TeleportToTarget(uint guardSerial, uint targetSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, guardSerial, out var resolvedGuard) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var resolvedTarget))
        {
            return false;
        }

        var guard = resolvedGuard!;
        var target = resolvedTarget!;

        return _movementModule.Teleport(
            guard,
            target.MapId,
            target.Location.X,
            target.Location.Y,
            target.Location.Z
        );
    }

    [ScriptFunction("try_reveal", "Attempts to reveal a hidden target mobile for the provided guard.")]
    public bool TryReveal(uint guardSerial, uint targetSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, guardSerial, out var resolvedGuard) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var resolvedTarget))
        {
            return false;
        }

        var guard = resolvedGuard!;
        var target = resolvedTarget!;

        if (guard.MapId != target.MapId || !target.IsHidden)
        {
            return false;
        }

        target.IsHidden = false;

        return true;
    }

    private sealed class NullSpeechService : ISpeechService
    {
        public static readonly NullSpeechService Instance = new();

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
        {
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

            return Task.FromResult(0);
        }

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = packet;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = speechPacket;
            _ = cancellationToken;

            return Task.FromResult<UnicodeSpeechMessagePacket?>(null);
        }

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
        {
            _ = session;
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

            return Task.FromResult(false);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
        {
            _ = speaker;
            _ = text;
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;

            return Task.FromResult(0);
        }
    }
}
