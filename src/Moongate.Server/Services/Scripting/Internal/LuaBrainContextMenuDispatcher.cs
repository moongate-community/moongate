using Moongate.Server.Data.Internal.Scripting;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Executes lua context-menu hooks for brains.
/// </summary>
internal static class LuaBrainContextMenuDispatcher
{
    private static readonly DynValue ContextMenuSelectedEventName = DynValue.NewString("context_menu_selected");

    public static IReadOnlyList<LuaBrainContextMenuEntry> GetEntries(
        Script? luaScript,
        LuaBrainRuntimeState state,
        UOMobileEntity? requester,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logger);

        if (luaScript is null || state.OnGetContextMenusFunction is null)
        {
            return [];
        }

        try
        {
            using var payload = LuaBrainContextMenuPayloadFactory.Rent(
                new LuaBrainContextMenuPayload(
                    state.MobileId,
                    requester,
                    0,
                    null
                )
            );
            var result = luaScript.Call(state.OnGetContextMenusFunction, payload.Payload);

            return LuaBrainContextMenuParser.Parse(result);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Lua get_context_menus failed for mobile {MobileId}", state.MobileId);

            return [];
        }
    }

    public static bool TryHandleSelection(
        Script? luaScript,
        LuaBrainRuntimeState state,
        UOMobileEntity? requester,
        string menuKey,
        long sessionId,
        ILogger logger
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(menuKey);
        ArgumentNullException.ThrowIfNull(logger);

        if (luaScript is null)
        {
            return false;
        }

        using var payload = LuaBrainContextMenuPayloadFactory.Rent(
            new LuaBrainContextMenuPayload(
                state.MobileId,
                requester,
                sessionId,
                menuKey
            )
        );

        try
        {
            if (state.OnSelectedContextMenuFunction is not null)
            {
                luaScript.Call(state.OnSelectedContextMenuFunction, menuKey, payload.Payload);

                return true;
            }

            if (state.OnEventFunction is not null)
            {
                var requesterId = requester is null ? 0u : (uint)requester.Id;
                luaScript.Call(state.OnEventFunction, ContextMenuSelectedEventName, requesterId, payload.Payload);

                return true;
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Lua on_selected_context_menu failed for mobile {MobileId} key {MenuKey}", state.MobileId, menuKey);
        }

        return false;
    }
}
