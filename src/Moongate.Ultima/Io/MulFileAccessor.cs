using System.Runtime.InteropServices;

namespace Moongate.Ultima.Io;

public class MulFileAccessor : IFileAccessor
{
    public Entry3D[] Index { get; }

    public long IdxLength { get; }

    public FileStream Stream { get; set; }

    public int IndexLength => Index.Length;

    public IEntry this[int index]
    {
        get => Index[index];
        set => Index[index] = (Entry3D)value;
    }

    public MulFileAccessor(string idxPath, string path, int length)
    {
        Index = new Entry3D[length];

        using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var count = (int)(index.Length / 12);
            IdxLength = index.Length;

            var readLen = (int)Math.Min(IdxLength, (long)Index.Length * 12);
            index.ReadExactly(MemoryMarshal.AsBytes(Index.AsSpan()).Slice(0, readLen));

            for (var i = count; i < Index.Length; ++i)
            {
                Index[i].Lookup = -1;
                Index[i].Length = -1;
                Index[i].Extra = -1;
            }
        }
    }

    public MulFileAccessor(string idxPath, string path)
    {
        using (var index = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var count = (int)(index.Length / 12);
            IdxLength = index.Length;
            Index = new Entry3D[count];
            index.ReadExactly(MemoryMarshal.AsBytes(Index.AsSpan()));
        }
    }

    public void ApplyPatch(Entry5D patch)
    {
        Index[patch.Index].Lookup = patch.Lookup;
        Index[patch.Index].Length = patch.Length | (1 << 31);
        Index[patch.Index].Extra = patch.Extra;
    }

    public IEntry GetEntry(int index)
    {
        if (index < 0 || index >= Index.Length)
        {
            return new Entry3D();
        }

        return Index[index];
    }
}
