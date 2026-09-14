using System.Buffers;
using Moongate.Core.Collections;

namespace Moongate.Tests.Core.Collections;

public class PooledRefListTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_GrowsAndTrimsInBothModes_PreservesItems(bool mt)
    {
        var list = new PooledRefList<int>(1, mt);

        try
        {
            for (var i = 0; i < 40; i++)
            {
                list.Add(i);
            }

            list.TrimExcess();
            Assert.Equal(40, list.Count);
            Assert.Equal(Enumerable.Range(0, 40), list.ToArray());
            var array = list.ToPooledArray();

            try
            {
                Assert.Equal(Enumerable.Range(0, 40), array.AsSpan(0, list.Count).ToArray());
            }
            finally
            {
                ArrayPool<int>.Shared.Return(array);
            }

            list.Clear();
            list.Capacity = 0;
            list.Add(99);
            Assert.Equal(99, list[0]);
        }
        finally
        {
            list.Dispose();
            list.Dispose();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructors_CopyCollections_KeepIndependentStorage(bool mt)
    {
        var source = new PooledRefList<string>(new[] { "first", "second" }, mt);

        try
        {
            using var copy = new PooledRefList<string>(source, mt);
            using var iteratorCopy = new PooledRefList<string>(Enumerable.Range(1, 2).Select(i => i.ToString()), mt);
            source[0] = "changed";

            Assert.Equal(new[] { "first", "second" }, copy.ToArray());
            Assert.Equal(new[] { "1", "2" }, iteratorCopy.ToArray());
        }
        finally
        {
            source.Dispose();
        }
    }
}
