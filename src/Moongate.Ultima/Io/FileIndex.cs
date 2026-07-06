using Moongate.Ultima.Interfaces;
namespace Moongate.Ultima.Io;

public sealed class FileIndex : IDisposable
{
    public IFileAccessor FileAccessor { get; }

    public long IndexLength => FileAccessor?.IndexLength ?? 0;
    public long IdxLength => FileAccessor?.IdxLength ?? 0;

    public IEntry this[int index]
    {
        get => FileAccessor[index];
        set => FileAccessor[index] = (Entry6D)value;
    }

    /// <summary>
    /// Absolute path to the .mul or .uop file backing this index, or null
    /// if no client file was located. Exposed so parallel preloaders can
    /// open their own per-thread FileStreams (FileShare.Read).
    /// </summary>
    public string MulPath { get; }

    public FileIndex(string idxFile, string mulFile, int length, int file) : this(
        idxFile,
        mulFile,
        null,
        length,
        file,
        ".dat",
        -1,
        false
    ) { }

    public FileIndex(
        string idxFile,
        string mulFile,
        string uopFile,
        int length,
        int file,
        string uopEntryExtension,
        int idxLength,
        bool hasExtra
    )
    {
        string idxPath = null;
        string uopPath = null;

        MulPath = null;

        if (Files.MulPath == null)
        {
            Files.LoadMulPath();
        }

        if (Files.MulPath.Count > 0)
        {
            idxPath = Files.MulPath[idxFile];
            MulPath = Files.MulPath[mulFile];

            if (!string.IsNullOrEmpty(uopFile) && Files.MulPath.ContainsKey(uopFile))
            {
                uopPath = Files.MulPath[uopFile];
            }

            if (string.IsNullOrEmpty(idxPath))
            {
                idxPath = null;
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetDirectoryName(idxPath)))
                {
                    idxPath = Path.Combine(Files.RootDir, idxPath);
                }

                if (!File.Exists(idxPath))
                {
                    idxPath = null;
                }
            }

            if (string.IsNullOrEmpty(MulPath))
            {
                MulPath = null;
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetDirectoryName(MulPath)))
                {
                    MulPath = Path.Combine(Files.RootDir, MulPath);
                }

                if (!File.Exists(MulPath))
                {
                    MulPath = null;
                }
            }

            if (!string.IsNullOrEmpty(uopPath))
            {
                if (string.IsNullOrEmpty(Path.GetDirectoryName(uopPath)))
                {
                    uopPath = Path.Combine(Files.RootDir, uopPath);
                }

                if (File.Exists(uopPath))
                {
                    MulPath = uopPath;
                }
            }
        }

        /* UOP files support code, written by Wyatt (c) www.ruosi.org
         * idxLength variable was added for compatibility with legacy code for art (see art.cs)
         * At the moment the only UOP file having entries with extra field is gumpartlegacy.uop,
         * and it's two DWORDs in the beginning of the entry.
         * It's possible that UOP can include some entries with unknown hash: not really unknown for me, but
         * not useful for reading legacy entries. That's why i removed unknown hash exception throwing from this code
         */
        if (MulPath?.EndsWith(".uop") == true)
        {
            FileAccessor = new UopFileAccessor(MulPath, uopEntryExtension, length, idxLength, hasExtra);
        }
        else if (idxPath != null && MulPath != null)
        {
            FileAccessor = new MulFileAccessor(idxPath, MulPath, length);
        }
        else
        {
            return;
        }

        if (file <= -1)
        {
            return;
        }

        var verdataPatches = Verdata.Patches;

        foreach (var patch in verdataPatches)
        {
            if (patch.File != file || patch.Index < 0 || patch.Index >= length)
            {
                continue;
            }

            FileAccessor.ApplyPatch(patch);
        }
    }

    public FileIndex(string idxFile, string mulFile, int file)
    {
        string idxPath = null;
        MulPath = null;

        if (Files.MulPath == null)
        {
            Files.LoadMulPath();
        }

        if (Files.MulPath.Count > 0)
        {
            idxPath = Files.MulPath[idxFile];
            MulPath = Files.MulPath[mulFile];

            if (string.IsNullOrEmpty(idxPath))
            {
                idxPath = null;
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetDirectoryName(idxPath)))
                {
                    idxPath = Path.Combine(Files.RootDir, idxPath);
                }

                if (!File.Exists(idxPath))
                {
                    idxPath = null;
                }
            }

            if (string.IsNullOrEmpty(MulPath))
            {
                MulPath = null;
            }
            else
            {
                if (string.IsNullOrEmpty(Path.GetDirectoryName(MulPath)))
                {
                    MulPath = Path.Combine(Files.RootDir, MulPath);
                }

                if (!File.Exists(MulPath))
                {
                    MulPath = null;
                }
            }
        }

        if (idxPath != null && MulPath != null)
        {
            FileAccessor = new MulFileAccessor(idxPath, MulPath);
        }
        else
        {
            return;
        }

        if (file <= -1)
        {
            return;
        }

        foreach (var patch in Verdata.Patches)
        {
            if (patch.File != file || patch.Index < 0 || patch.Index >= FileAccessor.IndexLength)
            {
                continue;
            }

            FileAccessor.ApplyPatch(patch);
        }
    }

    /// <summary>
    /// Releases the underlying .mul / .uop FileStream so the next access
    /// re-opens fresh. Additive — existing code paths that ignore the
    /// disposable contract keep working because EnsureOpen handles a
    /// disposed FileAccessor.Stream gracefully.
    /// </summary>
    public void Dispose()
    {
        FileAccessor?.Stream?.Dispose();

        if (FileAccessor != null)
        {
            FileAccessor.Stream = null;
        }
    }

    public Stream Seek(int index, out int length, out int extra, out bool patched)
    {
        if (FileAccessor is null)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        if (index < 0 || index >= FileAccessor.IndexLength)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        var e = FileAccessor.GetEntry(index);

        if (e.Lookup < 0 || e.Lookup > 0 && e.Length == -1)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        length = e.Length & 0x7FFFFFFF;
        extra = e.Extra;

        if ((e.Length & (1 << 31)) != 0)
        {
            patched = true;
            Verdata.Seek(e.Lookup);

            return Verdata.Stream;
        }

        if (e.Length < 0)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        var stream = EnsureOpen();

        if (stream == null)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        if (stream.Length < e.Lookup)
        {
            length = extra = 0;
            patched = false;

            return null;
        }

        patched = false;

        stream.Seek(e.Lookup, SeekOrigin.Begin);

        return stream;
    }

    public Stream Seek(int index, ref IEntry entry, out bool patched)
    {
        if (FileAccessor is null)
        {
            patched = false;

            return null;
        }

        if (index < 0 || index >= FileAccessor.IndexLength)
        {
            patched = false;

            return null;
        }

        var e = FileAccessor.GetEntry(index);

        if (e.Lookup < 0)
        {
            patched = false;

            return null;
        }

        var length = e.Length & 0x7FFFFFFF;

        if (length < 0)
        {
            patched = false;

            return null;
        }

        entry = e;

        if ((e.Length & (1 << 31)) != 0)
        {
            patched = true;
            Verdata.Seek(e.Lookup);

            return Verdata.Stream;
        }

        if (e.Length < 0)
        {
            patched = false;

            return null;
        }

        var stream = EnsureOpen();

        if (stream == null)
        {
            patched = false;

            return null;
        }

        if (stream.Length < e.Lookup)
        {
            patched = false;

            return null;
        }

        patched = false;

        stream.Seek(e.Lookup, SeekOrigin.Begin);

        return stream;
    }

    public bool Valid(int index, out int length, out int extra, out bool patched)
    {
        if (FileAccessor is null)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        if (index < 0 || index >= FileAccessor.IndexLength)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        var e = FileAccessor.GetEntry(index);

        if (e.Lookup < 0)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        length = e.Length & 0x7FFFFFFF;
        extra = e.Extra;

        if ((e.Length & (1 << 31)) != 0)
        {
            patched = true;

            return true;
        }

        if (e.Length < 0)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        if (MulPath == null || !File.Exists(MulPath))
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        var stream = EnsureOpen();

        if (stream == null)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        if (stream.Length < e.Lookup)
        {
            length = extra = 0;
            patched = false;

            return false;
        }

        patched = false;

        return true;
    }

    /// <summary>
    /// Returns the cached FileAccessor.Stream, re-opening it only when
    /// genuinely required (null or disposed). Replaces the per-call
    /// CanRead/CanSeek probe that previously re-instantiated the
    /// FileStream every time a downstream caller had Close()'d it.
    /// </summary>
    private FileStream EnsureOpen()
    {
        var stream = FileAccessor.Stream;

        if (stream != null && stream.CanRead && stream.CanSeek)
        {
            return stream;
        }

        if (MulPath == null)
        {
            FileAccessor.Stream = null;

            return null;
        }

        stream = new(MulPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        FileAccessor.Stream = stream;

        return stream;
    }
}
