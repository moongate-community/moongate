namespace Moongate.Server.Ultima.Modules.Internal;

/// <summary>
///     What a script can keep in the props of an NPC or an item: a string, a bool or a number, a whole number stored as a
///     long as the JSONB column gives it back. Tables and functions cannot be kept.
/// </summary>
internal static class ScriptPropValue
{
    public static bool TryFromLua(object value, out object prop)
    {
        switch (value)
        {
            case string or bool:
                prop = value;

                return true;
            case double number when double.IsFinite(number):
                // Boxed apart: a conditional of long and double would turn the long back into a double.
                prop = Math.Floor(number) == number && number is >= long.MinValue and <= long.MaxValue
                    ? (object)(long)number
                    : number;

                return true;
            default:
                // Safe: out parameter; callers read it only when the method returns true.
                prop = null!;

                return false;
        }
    }
}
