using System.Collections.Generic;
using System.IO;
using Moongate.Ultima.Imaging;
using SkiaSharp;

namespace Moongate.Ultima;

public sealed class AnimIdx
{
    public readonly int PaletteCapacity = 0x100;

    private readonly int _idxExtra;

    public ushort[] Palette { get; private set; }
    public List<FrameEdit> Frames { get; private set; }

    public AnimIdx(int index, FileIndex fileIndex)
    {
        Palette = new ushort[PaletteCapacity];

        Stream stream = fileIndex.Seek(index, out int length, out int extra, out bool _);
        if ((stream == null) || (length < 1))
        {
            return;
        }

        _idxExtra = extra;

        // leaveOpen: stream is owned by the shared FileIndex; disposing the
        // BinaryReader must not close it, or the next FileIndex.Seek pays a
        // full re-open.
        using (var bin = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < PaletteCapacity; ++i)
            {
                Palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
            }

            var start = (int)bin.BaseStream.Position;
            int frameCount = bin.ReadInt32();

            var lookups = new int[frameCount];

            for (int i = 0; i < frameCount; ++i)
            {
                lookups[i] = start + bin.ReadInt32();
            }

            Frames = new List<FrameEdit>();

            for (int i = 0; i < frameCount; ++i)
            {
                stream.Seek(lookups[i], SeekOrigin.Begin);
                Frames.Add(new FrameEdit(bin));
            }
        }
    }

    public AnimIdx(BinaryReader bin, int extra)
    {
        _idxExtra = extra;

        Palette = new ushort[PaletteCapacity];
        for (int i = 0; i < PaletteCapacity; ++i)
        {
            Palette[i] = (ushort)(bin.ReadUInt16() ^ 0x8000);
        }

        var start = (int)bin.BaseStream.Position;
        int frameCount = bin.ReadInt32();

        var lookups = new int[frameCount];

        for (int i = 0; i < frameCount; ++i)
        {
            lookups[i] = start + bin.ReadInt32();
        }

        Frames = new List<FrameEdit>();

        for (int i = 0; i < frameCount; ++i)
        {
            bin.BaseStream.Seek(lookups[i], SeekOrigin.Begin);
            Frames.Add(new FrameEdit(bin));
        }
    }

    public unsafe UltimaBitmap[] GetFrames()
    {
        if ((Frames == null) || (Frames.Count == 0))
        {
            return null;
        }

        var bits = new UltimaBitmap[Frames.Count];
        for (int i = 0; i < bits.Length; ++i)
        {
            FrameEdit frame = Frames[i];
            int width = frame.Width;
            int height = frame.Height;
            if (height == 0 || width == 0)
            {
                continue;
            }

            var bmp = new UltimaBitmap(width, height);
            var line = (ushort*)bmp.Scan0;
            int delta = bmp.Stride >> 1;

            int xBase = frame.Center.X - 0x200;
            int yBase = frame.Center.Y + height - 0x200;

            line += xBase;
            line += yBase * delta;

            for (int j = 0; j < frame.RawData.Length; ++j)
            {
                FrameEdit.Raw raw = frame.RawData[j];

                ushort* cur = line + (((raw.offsetY) * delta) + ((raw.offsetX) & 0x3FF));
                ushort* end = cur + (raw.run);

                int ii = 0;
                while (cur < end)
                {
                    *cur++ = Palette[raw.data[ii++]];
                }
            }

            bits[i] = bmp;
        }

        return bits;
    }

    public void AddFrame(UltimaBitmap bit, int centerX = 0, int centerY = 0)
    {
        if (Frames == null)
        {
            Frames = new List<FrameEdit>();
        }

        Frames.Add(new FrameEdit(bit, Palette, centerX, centerY));
    }

    public void ReplaceFrame(UltimaBitmap bit, int index)
    {
        if ((Frames == null) || (Frames.Count == 0))
        {
            return;
        }

        if (index > Frames.Count)
        {
            return;
        }

        Frames[index] = new FrameEdit(bit, Palette, Frames[index].Center.X, Frames[index].Center.Y);
    }

    public void RemoveFrame(int index)
    {
        if (Frames == null)
        {
            return;
        }

        if (index > Frames.Count)
        {
            return;
        }

        Frames.RemoveAt(index);
    }

    public void ClearFrames()
    {
        Frames?.Clear();
    }

    public void ExportPalette(string filename, int type)
    {
        switch (type)
        {
            case 0:
                using (var tex = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.ReadWrite)))
                {
                    for (int i = 0; i < PaletteCapacity; ++i)
                    {
                        tex.WriteLine(Palette[i]);
                    }
                }
                break;
            case 1:
                SavePaletteImage(filename);
                break;
            case 2:
                SavePaletteImage(filename);
                break;
        }
    }

    // Skia cannot encode BMP/TIFF, so the palette image is always written as PNG
    // regardless of the requested export type.
    private unsafe void SavePaletteImage(string filename)
    {
        using (var bmp = new UltimaBitmap(PaletteCapacity, 20))
        {
            var line = (ushort*)bmp.Scan0;
            int delta = bmp.Stride >> 1;

            for (int y = 0; y < bmp.Height; ++y, line += delta)
            {
                ushort* cur = line;
                for (int i = 0; i < PaletteCapacity; ++i)
                {
                    *cur++ = Palette[i];
                }
            }

            bmp.Save(filename, SKEncodedImageFormat.Png);
        }
    }

    public void ReplacePalette(ushort[] palette)
    {
        Palette = palette;
    }

    public void Save(BinaryWriter bin, BinaryWriter idx)
    {
        if ((Frames == null) || (Frames.Count == 0))
        {
            idx.Write(-1);
            idx.Write(-1);
            idx.Write(-1);

            return;
        }

        long start = bin.BaseStream.Position;
        idx.Write((int)start);

        for (int i = 0; i < PaletteCapacity; ++i)
        {
            bin.Write((ushort)(Palette[i] ^ 0x8000));
        }

        long startPosition = bin.BaseStream.Position;
        bin.Write(Frames.Count);

        long seek = bin.BaseStream.Position;
        long curr = bin.BaseStream.Position + (4 * Frames.Count);

        foreach (FrameEdit frame in Frames)
        {
            bin.BaseStream.Seek(seek, SeekOrigin.Begin);
            bin.Write((int)(curr - startPosition));
            seek = bin.BaseStream.Position;
            bin.BaseStream.Seek(curr, SeekOrigin.Begin);
            frame.Save(bin);
            curr = bin.BaseStream.Position;
        }

        start = bin.BaseStream.Position - start;
        idx.Write((int)start);
        idx.Write(_idxExtra);
    }

    public void ExportToVD(BinaryWriter bin, ref long indexpos, ref long animpos)
    {
        bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
        if ((Frames == null) || (Frames.Count == 0))
        {
            bin.Write(-1);
            bin.Write(-1);
            bin.Write(-1);
            indexpos = bin.BaseStream.Position;
            return;
        }

        bin.Write((int)animpos);
        indexpos = bin.BaseStream.Position;
        bin.BaseStream.Seek(animpos, SeekOrigin.Begin);

        for (int i = 0; i < PaletteCapacity; ++i)
        {
            bin.Write((ushort)(Palette[i] ^ 0x8000));
        }

        long startPosition = (int)bin.BaseStream.Position;
        bin.Write(Frames.Count);
        long seek = (int)bin.BaseStream.Position;
        long curr = bin.BaseStream.Position + (4 * Frames.Count);
        foreach (FrameEdit frame in Frames)
        {
            bin.BaseStream.Seek(seek, SeekOrigin.Begin);
            bin.Write((int)(curr - startPosition));
            seek = bin.BaseStream.Position;
            bin.BaseStream.Seek(curr, SeekOrigin.Begin);
            frame.Save(bin);
            curr = bin.BaseStream.Position;
        }

        long length = bin.BaseStream.Position - animpos;
        animpos = bin.BaseStream.Position;
        bin.BaseStream.Seek(indexpos, SeekOrigin.Begin);
        bin.Write((int)length);
        bin.Write(_idxExtra);
        indexpos = bin.BaseStream.Position;
    }
}
