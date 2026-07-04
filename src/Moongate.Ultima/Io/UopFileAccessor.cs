using System.Text;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Types;

namespace Moongate.Ultima.Io;

public class UopFileAccessor : IFileAccessor
{
    public Entry6D[] Index { get; }

    public FileStream Stream { get; set; }

    public long IdxLength { get; }

    public int IndexLength => Index.Length;

    public IEntry this[int index]
    {
        get => Index[index];
        set => Index[index] = (Entry6D)value;
    }

    public UopFileAccessor(string path, string uopEntryExtension, int length, int idxLength, bool hasextra)
    {
        Index = new Entry6D[length];

        if (idxLength > 0)
        {
            IdxLength = idxLength * 12;
        }

        Stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        var fileInfo = new FileInfo(path);
        var uopPattern = fileInfo.Name.Replace(fileInfo.Extension, "").ToLowerInvariant();

        // leaveOpen: this ctor caches Stream on the instance for later
        // FileIndex.Seek calls; disposing the BinaryReader must not close it.
        using (var br = new BinaryReader(Stream, Encoding.UTF8, true))
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);

            if (br.ReadInt32() != 0x50594D)
            {
                throw new ArgumentException("Bad UOP file.");
            }

            _ = br.ReadUInt32(); // version
            _ = br.ReadUInt32(); // signature
            var nextBlock = br.ReadInt64();
            _ = br.ReadUInt32(); // block size (capacity?)
            _ = br.ReadInt32();  // count

            var hashes = new Dictionary<ulong, int>();

            for (var i = 0; i < length; i++)
            {
                var entryName = $"build/{uopPattern}/{i:D8}{uopEntryExtension}";
                var hash = UopUtils.HashFileName(entryName);

                hashes.TryAdd(hash, i);
            }

            br.BaseStream.Seek(nextBlock, SeekOrigin.Begin);

            // There are no invalid entries in .uop so we have to initialize all entries
            // as invalid and then fill the valid ones
            for (var i = 0; i < Index.Length; i++)
            {
                Index[i].Lookup = -1;
                Index[i].Length = -1;
                Index[i].Extra = -1;
            }

            do
            {
                var filesCount = br.ReadInt32();
                nextBlock = br.ReadInt64();

                for (var i = 0; i < filesCount; i++)
                {
                    var offset = br.ReadInt64();
                    var headerLength = br.ReadInt32();
                    var compressedLength = br.ReadInt32();
                    var decompressedLength = br.ReadInt32();
                    var hash = br.ReadUInt64();
                    _ = br.ReadUInt32(); // data_hash
                    var flag = br.ReadInt16();

                    if (offset == 0)
                    {
                        continue;
                    }

                    if (!hashes.TryGetValue(hash, out var idx))
                    {
                        continue;
                    }

                    if (idx < 0 || idx > Index.Length)
                    {
                        throw new IndexOutOfRangeException(
                            "hashes dictionary and files collection have different count of entries!"
                        );
                    }

                    offset += headerLength;

                    if (hasextra && flag != 3)
                    {
                        var curPos = br.BaseStream.Position;

                        br.BaseStream.Seek(offset, SeekOrigin.Begin);

                        var extra1 = br.ReadInt32();
                        var extra2 = br.ReadInt32();
                        Index[idx].Lookup = (int)(offset + 8);
                        Index[idx].Length = compressedLength - 8;
                        Index[idx].DecompressedLength = decompressedLength;
                        Index[idx].Flag = (CompressionFlag)flag;
                        Index[idx].Extra = (extra1 << 16) | extra2;
                        Index[idx].Extra1 = extra1;
                        Index[idx].Extra2 = extra2;

                        br.BaseStream.Seek(curPos, SeekOrigin.Begin);
                    }
                    else
                    {
                        Index[idx].Lookup = (int)offset;
                        Index[idx].Length = compressedLength;
                        Index[idx].DecompressedLength = decompressedLength;
                        Index[idx].Flag = (CompressionFlag)flag;
                        Index[idx].Extra = 0x0FFFFFFF; // we cant read it right now, but -1 and 0 makes this entry invalid
                    }
                }
            } while (br.BaseStream.Seek(nextBlock, SeekOrigin.Begin) != 0);
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
            return new Entry6D();
        }

        return Index[index];
    }
}
