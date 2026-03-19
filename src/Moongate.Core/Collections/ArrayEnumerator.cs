using System.Collections;

namespace Moongate.Core.Collections;

/// <summary>
/// Non-thread safe, non-guarded enumerator for classes that have internal arrays.
/// Recommended to copy this and use it as a nested struct.
/// Recommend adding version checking to properly guard against modification during enumeration.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct ArrayEnumerator<T> : IEnumerator<T>
{
    private readonly T[] _array;
    private int _index;

    public ArrayEnumerator(T[] array)
    {
        _array = array;
        _index = 0;
        Current = default;
    }

    public T? Current { get; private set; }

    object IEnumerator.Current
    {
        get
        {
            if (_index == 0 || _index == _array.Length + 1)
            {
                throw new InvalidOperationException(nameof(_index));
            }

            return Current!;
        }
    }

    public void Dispose() { }

    public bool MoveNext()
    {
        var localList = _array;

        if ((uint)_index < (uint)localList.Length)
        {
            Current = localList[_index++];

            return true;
        }

        return false;
    }

    void IEnumerator.Reset()
    {
        _index = 0;
        Current = default;
    }
}
