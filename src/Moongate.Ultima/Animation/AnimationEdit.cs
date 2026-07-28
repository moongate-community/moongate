using Moongate.Ultima.Io;

namespace Moongate.Ultima.Animation;

public sealed class AnimationEdit
{
    private static FileIndex _fileIndex = new("Anim.idx", "Anim.mul", 6);
    private static FileIndex _fileIndex2 = new("Anim2.idx", "Anim2.mul", -1);
    private static FileIndex _fileIndex3 = new("Anim3.idx", "Anim3.mul", -1);
    private static FileIndex _fileIndex4 = new("Anim4.idx", "Anim4.mul", -1);
    private static FileIndex _fileIndex5 = new("Anim5.idx", "Anim5.mul", -1);
    private static FileIndex _fileIndex6 = new("Anim6.idx", "Anim6.mul", -1);

    private static AnimIdx[] _animCache;
    private static AnimIdx[] _animCache2;
    private static AnimIdx[] _animCache3;
    private static AnimIdx[] _animCache4;
    private static AnimIdx[] _animCache5;
    private static AnimIdx[] _animCache6;

    static AnimationEdit()
    {
        InitializeCache();
    }

    public static void ExportToVD(int fileType, int body, string file)
    {
        var cache = GetCache(fileType);
        GetFileIndex(body, fileType, 0, 0, out var fileIndex, out var index);

        using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var bin = new BinaryWriter(fs))
            {
                bin.Write((short)6);
                var animLength = Animations.GetAnimLength(body, fileType);
                var currType = animLength == 22 ? 0 :
                               animLength == 13 ? 1 : 2;
                bin.Write((short)currType);
                var indexPos = bin.BaseStream.Position;
                var animPos = bin.BaseStream.Position + 12 * animLength * 5;

                for (var i = index; i < index + animLength * 5; i++)
                {
                    AnimIdx anim;

                    if (cache != null)
                    {
                        anim = cache[i] != null ? cache[i] : cache[i] = new(i, fileIndex);
                    }
                    else
                    {
                        anim = cache[i] = new(i, fileIndex);
                    }

                    if (anim == null)
                    {
                        bin.BaseStream.Seek(indexPos, SeekOrigin.Begin);
                        bin.Write(-1);
                        bin.Write(-1);
                        bin.Write(-1);
                        indexPos = bin.BaseStream.Position;
                    }
                    else
                    {
                        anim.ExportToVD(bin, ref indexPos, ref animPos);
                    }
                }
            }
        }
    }

    public static AnimIdx GetAnimation(int fileType, int body, int action, int dir)
    {
        var cache = GetCache(fileType);

        GetFileIndex(body, fileType, action, dir, out var fileIndex, out var index);

        if (cache?[index] != null)
        {
            return cache[index];
        }

        return cache[index] = new(index, fileIndex);
    }

    public static bool IsActionDefined(int fileType, int body, int action)
    {
        // Reject actions beyond the body's physical idx block before computing
        // the index; otherwise index = base + action*5 crosses into the next
        // body's records. Replaces a prior off-by-one GetAnimLength check
        // (animCount < action) that both missed the boundary and used the
        // now-clamped category count.
        if (action < 0 || action >= Animations.GetActionCapacity(body, fileType))
        {
            return false;
        }

        var cache = GetCache(fileType);

        GetFileIndex(body, fileType, action, 0, out var fileIndex, out var index);

        if (cache?[index] != null)
        {
            return cache[index].Frames?.Count > 0;
        }

        var valid = fileIndex.Valid(index, out var length, out _, out _);

        return valid && length >= 1;
    }

    public static void LoadFromVD(int fileType, int body, BinaryReader bin)
    {
        var cache = GetCache(fileType);
        GetFileIndex(body, fileType, 0, 0, out _, out var index);
        var animLength = Animations.GetAnimLength(body, fileType) * 5;
        var entries = new Entry3D[animLength];

        for (var i = 0; i < animLength; ++i)
        {
            entries[i].Lookup = bin.ReadInt32();
            entries[i].Length = bin.ReadInt32();
            entries[i].Extra = bin.ReadInt32();
        }

        foreach (var entry in entries)
        {
            if (entry.Lookup > 0 && entry.Lookup < bin.BaseStream.Length && entry.Length > 0)
            {
                bin.BaseStream.Seek(entry.Lookup, SeekOrigin.Begin);
                cache[index] = new(bin, entry.Extra);
            }

            ++index;
        }
    }

    /// <summary>
    /// Rereads AnimX files
    /// </summary>
    public static void Reload()
    {
        _fileIndex = new("Anim.idx", "Anim.mul", 6);
        _fileIndex2 = new("Anim2.idx", "Anim2.mul", -1);
        _fileIndex3 = new("Anim3.idx", "Anim3.mul", -1);
        _fileIndex4 = new("Anim4.idx", "Anim4.mul", -1);
        _fileIndex5 = new("Anim5.idx", "Anim5.mul", -1);
        _fileIndex6 = new("Anim6.idx", "Anim6.mul", -1);

        InitializeCache();
    }

    public static void Save(int fileType, string path)
    {
        string filename;
        AnimIdx[] cache;
        FileIndex fileIndex;

        switch (fileType)
        {
            default:
            case 1:
                filename = "anim";
                cache = _animCache;
                fileIndex = _fileIndex;

                break;
            case 2:
                filename = "anim2";
                cache = _animCache2;
                fileIndex = _fileIndex2;

                break;
            case 3:
                filename = "anim3";
                cache = _animCache3;
                fileIndex = _fileIndex3;

                break;
            case 4:
                filename = "anim4";
                cache = _animCache4;
                fileIndex = _fileIndex4;

                break;
            case 5:
                filename = "anim5";
                cache = _animCache5;
                fileIndex = _fileIndex5;

                break;
            case 6:
                filename = "anim6";
                cache = _animCache6;
                fileIndex = _fileIndex6;

                break;
        }

        var idx = Path.Combine(path, filename + ".idx");
        var mul = Path.Combine(path, filename + ".mul");

        using (var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                using (var binidx = new BinaryWriter(fsidx))
                {
                    using (var binmul = new BinaryWriter(fsmul))
                    {
                        for (var idxc = 0; idxc < cache.Length; ++idxc)
                        {
                            AnimIdx anim;

                            if (cache != null)
                            {
                                anim = cache[idxc] != null ? cache[idxc] : cache[idxc] = new(idxc, fileIndex);
                            }
                            else
                            {
                                anim = cache[idxc] = new(idxc, fileIndex);
                            }

                            if (anim == null)
                            {
                                binidx.Write(-1);
                                binidx.Write(-1);
                                binidx.Write(-1);
                            }
                            else
                            {
                                anim.Save(binmul, binidx);
                            }
                        }
                    }
                }
            }
        }
    }

    private static AnimIdx[] GetCache(int fileType)
    {
        switch (fileType)
        {
            case 1:
                return _animCache;
            case 2:
                return _animCache2;
            case 3:
                return _animCache3;
            case 4:
                return _animCache4;
            case 5:
                return _animCache5;
            case 6:
                return _animCache6;
            default:
                return _animCache;
        }
    }

    private static void GetFileIndex(
        int body,
        int fileType,
        int action,
        int direction,
        out FileIndex fileIndex,
        out int index
    )
    {
        switch (fileType)
        {
            case 1:
            default:
                fileIndex = _fileIndex;

                if (body < 200)
                {
                    index = body * 110;
                }
                else if (body < 400)
                {
                    index = 22000 + (body - 200) * 65;
                }
                else
                {
                    index = 35000 + (body - 400) * 175;
                }

                break;
            case 2:
                fileIndex = _fileIndex2;

                if (body < 200)
                {
                    index = body * 110;
                }
                else
                {
                    index = 22000 + (body - 200) * 65;
                }

                break;
            case 3:
                fileIndex = _fileIndex3;

                if (body < 300)
                {
                    index = body * 65;
                }
                else if (body < 400)
                {
                    index = 33000 + (body - 300) * 110;
                }
                else
                {
                    index = 35000 + (body - 400) * 175;
                }

                break;
            case 4:
                fileIndex = _fileIndex4;

                if (body < 200)
                {
                    index = body * 110;
                }
                else if (body < 400)
                {
                    index = 22000 + (body - 200) * 65;
                }
                else
                {
                    index = 35000 + (body - 400) * 175;
                }

                break;
            case 5:
                fileIndex = _fileIndex5;

                if (body < 200 && body != 34) // looks strange, though it works.
                {
                    index = body * 110;
                }
                else if (body < 400)
                {
                    index = 22000 + (body - 200) * 65;
                }
                else
                {
                    index = 35000 + (body - 400) * 175;
                }

                break;
            case 6:
                fileIndex = _fileIndex6;

                if (body < 200)
                {
                    index = body * 110;
                }
                else if (body < 400)
                {
                    index = 22000 + (body - 200) * 65;
                }
                else
                {
                    index = 35000 + (body - 400) * 175;
                }

                break;
        }

        index += action * 5;

        if (direction <= 4)
        {
            index += direction;
        }
        else
        {
            index += direction - (direction - 4) * 2;
        }
    }

    private static void InitializeCache()
    {
        if (_fileIndex.IdxLength > 0)
        {
            _animCache = new AnimIdx[_fileIndex.IdxLength / 12];
        }

        if (_fileIndex2.IdxLength > 0)
        {
            _animCache2 = new AnimIdx[_fileIndex2.IdxLength / 12];
        }

        if (_fileIndex3.IdxLength > 0)
        {
            _animCache3 = new AnimIdx[_fileIndex3.IdxLength / 12];
        }

        if (_fileIndex4.IdxLength > 0)
        {
            _animCache4 = new AnimIdx[_fileIndex4.IdxLength / 12];
        }

        if (_fileIndex5.IdxLength > 0)
        {
            _animCache5 = new AnimIdx[_fileIndex5.IdxLength / 12];
        }

        if (_fileIndex6.IdxLength > 0)
        {
            _animCache6 = new AnimIdx[_fileIndex6.IdxLength / 12];
        }
    }
}
