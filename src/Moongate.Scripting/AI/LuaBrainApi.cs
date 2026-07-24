using MoonSharp.Interpreter;

namespace Moongate.Scripting.AI;

internal static class LuaBrainApi
{
    public static Table Create(Script script)
    {
        var brain = new Table(script);
        brain.Set("idle", CreateIntentCallback(script, "idle"));
        brain.Set("say", CreateIntentCallback(script, "say", "text"));
        brain.Set("patrol", CreateIntentCallback(script, "patrol"));
        brain.Set("move_toward", CreateIntentCallback(script, "move_toward", "target_id"));
        brain.Set("move_away", CreateIntentCallback(script, "move_away", "target_id"));
        brain.Set("engage", CreateIntentCallback(script, "engage", "target_id"));
        brain.Set("clear_target", CreateIntentCallback(script, "clear_target"));
        brain.Set("return_home", CreateIntentCallback(script, "return_home"));
        brain.Set(
            "decision",
            DynValue.NewCallback(
                (_, arguments) =>
                {
                    var decision = new Table(script);
                    decision.Set("next_tick_ms", GetArgument(arguments, 0));
                    decision.Set("intents", GetArgument(arguments, 1));

                    return DynValue.NewTable(decision);
                }
            )
        );

        return brain;
    }

    private static DynValue CreateIntentCallback(Script script, string intentType, string? payloadName = null)
        => DynValue.NewCallback(
            (_, arguments) =>
            {
                var intent = new Table(script);
                intent.Set("type", DynValue.NewString(intentType));

                if (payloadName is not null)
                {
                    intent.Set(payloadName, GetArgument(arguments, 0));
                }

                return DynValue.NewTable(intent);
            }
        );

    private static DynValue GetArgument(CallbackArguments arguments, int index)
        => index < arguments.Count ? arguments[index] : DynValue.Nil;
}
