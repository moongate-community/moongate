namespace Moongate.Scripting.Data.Binding;

/// <summary>
///     A script function's signature in Lua terms: what the editor definitions and the documentation both show.
/// </summary>
/// <param name="LuaName">
///     The name scripts call the function by.
/// </param>
/// <param name="HelpText">
///     The function's help text, if any.
/// </param>
/// <param name="Parameters">
///     The parameters in call order.
/// </param>
/// <param name="Returns">
///     The Lua type returned, ending with <c>?</c> when it may be nil; null for a function that returns nothing.
/// </param>
public sealed record FunctionDescription(
    string LuaName,
    string? HelpText,
    IReadOnlyList<ParameterDescription> Parameters,
    string? Returns
);
