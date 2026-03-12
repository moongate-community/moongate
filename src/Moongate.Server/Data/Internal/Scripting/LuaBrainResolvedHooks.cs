using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Resolved Lua hook references for a brain table.
/// </summary>
public sealed record LuaBrainResolvedHooks(
    DynValue? BrainLoopFunction,
    DynValue? OnSpeechFunction,
    DynValue? OnDeathFunction,
    DynValue? OnSpawnFunction,
    DynValue? OnInRangeFunction,
    DynValue? OnOutRangeFunction,
    DynValue? OnGetContextMenusFunction,
    DynValue? OnSelectedContextMenuFunction,
    DynValue? OnEventFunction
);
