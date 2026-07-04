namespace Moongate.Ultima.Io;

public sealed class Files
{
    public delegate void FileSaveHandler();

    public static event FileSaveHandler FileSaveEvent;

    /// <summary>
    /// Should loaded Data be cached
    /// </summary>
    public static bool CacheData { get; set; } = true;

    /// <summary>
    /// Initial LRU capacity for the Art read cache (statics + land
    /// tiles share the same cache). Default 4096 — bounds the worst-case
    /// working set to a few hundred MB of bitmaps even after a full
    /// 0x14000-id scan, while keeping recent thumbnails warm. Reading
    /// happens at static-ctor time so set this before first use, or call
    /// <see cref="Ultima.Art.SetCacheCapacity" /> at runtime.
    /// </summary>
    public static int CacheCapacityArt { get; set; } = 4096;

    /// <summary>
    /// Initial LRU capacity for the Gumps read cache. Default 2048 —
    /// gumps are larger on average than statics, so the cap is lower
    /// to keep total memory comparable. Adjust via
    /// <see cref="Ultima.Gumps.SetCacheCapacity" /> at runtime.
    /// </summary>
    public static int CacheCapacityGumps { get; set; } = 2048;

    /// <summary>
    /// Initial LRU capacity for the Animations frame cache (the only major
    /// file format previously without a decode cache). Counts whole
    /// AnimationFrame[] entries — thumbnails are 1 frame, player directions
    /// a handful. Default 1024 keeps the visible grid + scroll working set
    /// warm. Adjust via <see cref="Ultima.Animations.SetCacheCapacity" /> at
    /// runtime.
    /// </summary>
    public static int CacheCapacityAnimations { get; set; } = 1024;

    /// <summary>
    /// Contains the path infos
    /// </summary>
    public static Dictionary<string, string> MulPath { get; set; }

    /// <summary>
    /// Gets the path to the client's data files directory. Must be set explicitly
    /// via <see cref="SetDirectory" /> or <see cref="SetMulPath(string)" /> before use.
    /// </summary>
    public static string Directory { get; private set; }

    /// <summary>
    /// Contains the rootDir (so relative values are possible for <see cref="MulPath" />
    /// </summary>
    public static string RootDir { get; set; }

    private static readonly string[] _uoFiles =
    [
        "anim.idx",
        "anim.mul",
        "anim2.idx",
        "anim2.mul",
        "anim3.idx",
        "anim3.mul",
        "anim4.idx",
        "anim4.mul",
        "anim5.idx",
        "anim5.mul",
        "anim6.idx",
        "anim6.mul",
        "animdata.mul",
        "animationframe1.uop",
        "animationframe2.uop",
        "animationframe3.uop",
        "animationframe4.uop",
        "animationframe5.uop",
        "animationframe6.uop",
        "animationsequence.uop",
        "art.mul",
        "artidx.mul",
        "artlegacymul.uop",
        "body.def",
        "bodyconv.def",
        "client.exe",
        "cliloc.custom1",
        "cliloc.custom2",
        "cliloc.deu",
        "cliloc.enu",
        "equipconv.def",
        "facet00.mul",
        "facet01.mul",
        "facet02.mul",
        "facet03.mul",
        "facet04.mul",
        "facet05.mul",
        "fonts.mul",
        "gump.def",
        "gumpart.mul",
        "gumpidx.mul",
        "gumpartlegacymul.uop",
        "hues.mul",
        "light.mul",
        "lightidx.mul",
        "map0.mul",
        "map1.mul",
        "map2.mul",
        "map3.mul",
        "map4.mul",
        "map5.mul",
        "map0legacymul.uop",
        "map1legacymul.uop",
        "map2legacymul.uop",
        "map3legacymul.uop",
        "map4legacymul.uop",
        "map5legacymul.uop",
        "mapdif0.mul",
        "mapdif1.mul",
        "mapdif2.mul",
        "mapdif3.mul",
        "mapdif4.mul",
        "mapdifl0.mul",
        "mapdifl1.mul",
        "mapdifl2.mul",
        "mapdifl3.mul",
        "mapdifl4.mul",
        "mobtypes.txt",
        "multi.idx",
        "multi.mul",
        "multicollection.uop",
        "multimap.rle",
        "radarcol.mul",
        "skillgrp.mul",
        "skills.idx",
        "skills.mul",
        "sound.def",
        "sound.mul",
        "soundidx.mul",
        "soundlegacymul.uop",
        "speech.mul",
        "stadif0.mul",
        "stadif1.mul",
        "stadif2.mul",
        "stadif3.mul",
        "stadif4.mul",
        "stadifi0.mul",
        "stadifi1.mul",
        "stadifi2.mul",
        "stadifi3.mul",
        "stadifi4.mul",
        "stadifl0.mul",
        "stadifl1.mul",
        "stadifl2.mul",
        "stadifl3.mul",
        "stadifl4.mul",
        "staidx0.mul",
        "staidx1.mul",
        "staidx2.mul",
        "staidx3.mul",
        "staidx4.mul",
        "staidx5.mul",
        "statics0.mul",
        "statics1.mul",
        "statics2.mul",
        "statics3.mul",
        "statics4.mul",
        "statics5.mul",
        "texidx.mul",
        "texmaps.mul",
        "tiledata.mul",
        "unifont.mul",
        "unifont1.mul",
        "unifont2.mul",
        "unifont3.mul",
        "unifont4.mul",
        "unifont5.mul",
        "unifont6.mul",
        "unifont7.mul",
        "unifont8.mul",
        "unifont9.mul",
        "unifont10.mul",
        "unifont11.mul",
        "unifont12.mul",
        "uotd.exe",
        "verdata.mul"
    ];

    static Files()
    {
        LoadMulPath();
    }

    public static void FireFileSaveEvent()
        => FileSaveEvent?.Invoke();

    /// <summary>
    /// Looks up a given <paramref name="file" /> in <see cref="Files.MulPath" />
    /// </summary>
    /// <returns>
    /// The absolute path to <paramref name="file" /> -or- <c>null</c> if <paramref name="file" /> was not found.
    /// </returns>
    public static string GetFilePath(string file)
    {
        if (MulPath.Count == 0)
        {
            return null;
        }

        var path = string.Empty;

        if (MulPath.TryGetValue(file, out var mapped))
        {
            path = mapped;
        }

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (string.IsNullOrEmpty(Path.GetDirectoryName(path)))
        {
            path = Path.Combine(RootDir, path);
        }

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Fills <see cref="MulPath" /> with <see cref="Files.Directory" />. File names are
    /// resolved case-insensitively so mixed-case client files (e.g. map0LegacyMUL.uop)
    /// are found on case-sensitive filesystems.
    /// </summary>
    public static void LoadMulPath()
    {
        MulPath = new(StringComparer.OrdinalIgnoreCase);
        RootDir = Directory ?? string.Empty;

        var onDisk = BuildCaseInsensitiveFileMap(RootDir);

        foreach (var file in _uoFiles)
        {
            MulPath[file] = onDisk.TryGetValue(file, out var actual) ? actual : string.Empty;
        }
    }

    /// <summary>
    /// Sets the client data directory explicitly and reloads <see cref="MulPath" /> from it.
    /// </summary>
    /// <param name="path">Absolute path to the UO client files directory.</param>
    public static void SetDirectory(string path)
    {
        Directory = path;
        LoadMulPath();
    }

    /// <summary>
    /// ReSets <see cref="MulPath" /> with given path
    /// </summary>
    /// <param name="path"></param>
    public static void SetMulPath(string path)
    {
        RootDir = path;

        var onDisk = BuildCaseInsensitiveFileMap(RootDir);

        foreach (var file in _uoFiles)
        {
            // file was set
            if (!string.IsNullOrEmpty(MulPath[file]))
            {
                // and was relative like "art.mul"
                if (string.IsNullOrEmpty(Path.GetDirectoryName(MulPath[file])))
                {
                    if (onDisk.TryGetValue(MulPath[file], out var mapped))
                    {
                        MulPath[file] = Path.Combine(RootDir, mapped);

                        continue;
                    }
                }
                else
                {
                    // absolute dir
                    // ignore because someone might want custom path for individual file
                    continue;
                }
            }

            // file was not set, or relative and non existent
            MulPath[file] = onDisk.TryGetValue(file, out var actual) ? Path.Combine(RootDir, actual) : string.Empty;
        }
    }

    /// <summary>
    /// Sets <see cref="MulPath" /> key to path
    /// </summary>
    /// <param name="path"></param>
    /// <param name="key"></param>
    public static void SetMulPath(string path, string key)
        => MulPath[key] = path;

    /// <summary>
    /// Maps lowercase file names to their actual on-disk names for <paramref name="directory" />,
    /// enabling case-insensitive lookups on case-sensitive filesystems.
    /// </summary>
    private static Dictionary<string, string> BuildCaseInsensitiveFileMap(string directory)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
        {
            return map;
        }

        foreach (var path in System.IO.Directory.EnumerateFiles(directory))
        {
            map[Path.GetFileName(path)] = Path.GetFileName(path);
        }

        return map;
    }
}
