using Moongate.Ultima.Io;
namespace Moongate.Ultima.Interfaces;

public interface IFileAccessor
{
    FileStream Stream { get; set; }
    int IndexLength { get; }
    long IdxLength { get; }
    IEntry this[int index] { get; set; }
    void ApplyPatch(Entry5D patch);
    IEntry GetEntry(int index);
}
