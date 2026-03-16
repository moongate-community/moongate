using System.Collections.Concurrent;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Small dictionary pool used by Lua brain payload factories.
/// </summary>
internal static class LuaBrainDictionaryPool
{
    private static readonly ConcurrentBag<Dictionary<string, object?>> ObjectPool = new();
    private static readonly ConcurrentBag<Dictionary<string, int>> IntPool = new();

    public static Dictionary<string, int> RentIntDictionary()
        => IntPool.TryTake(out var value) ? value : new(4);

    public static Dictionary<string, object?> RentObjectDictionary()
        => ObjectPool.TryTake(out var value) ? value : new(8);

    public static void Return(Dictionary<string, object?> dictionary)
    {
        dictionary.Clear();
        ObjectPool.Add(dictionary);
    }

    public static void Return(Dictionary<string, int> dictionary)
    {
        dictionary.Clear();
        IntPool.Add(dictionary);
    }
}
