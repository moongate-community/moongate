using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Moongate.Core.Collections;

public static class CollectionHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddNotNull<T>(this ICollection<T> coll, T t) where T : class
    {
        if (t != null)
        {
            coll.Add(t);
        }
    }
}
