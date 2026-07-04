namespace Moongate.Ultima.Io;

public interface IFileAccessor
{
    FileStream Stream { get; set; }
    int IndexLength { get; }
    long IdxLength { get; }
    IEntry this[int index] { get; set; }
    void ApplyPatch(Entry5D patch);
    IEntry GetEntry(int index);
}
