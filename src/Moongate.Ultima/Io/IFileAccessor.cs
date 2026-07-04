using System.IO;

namespace Moongate.Ultima.Io;

public interface IFileAccessor
{
    public IEntry GetEntry(int index);
    void ApplyPatch(Entry5D patch);
    public FileStream Stream { get; set; }
    public int IndexLength { get; }
    public long IdxLength { get; }
    public IEntry this[int index] { get; set; }
}
