using MoonSharp.Interpreter;

namespace Moongate.Server.Data.Internal.Scripting;

/// <summary>
/// Resolved Lua hook references for a brain table.
/// </summary>
public sealed record LuaBrainResolvedHooks(
    DynValue? OnThinkFunction,
    DynValue? OnSpeechFunction,
    DynValue? OnBeforeDeathFunction,
    DynValue? OnDeathFunction,
    DynValue? OnAfterDeathFunction,
    DynValue? OnSpawnFunction,
    DynValue? OnAttackFunction,
    DynValue? OnMissedAttackFunction,
    DynValue? OnAttackedFunction,
    DynValue? OnMissedByAttackFunction,
    DynValue? OnInRangeFunction,
    DynValue? OnOutRangeFunction,
    DynValue? OnGetContextMenusFunction,
    DynValue? OnSelectedContextMenuFunction,
    DynValue? OnEventFunction
);
