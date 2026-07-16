using System.Text;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Skill;

public sealed class Skills
{
    private static FileIndex _fileIndex = new("skills.idx", "skills.mul", 16);

    private static List<SkillInfo> _skillEntries;

    public static List<SkillInfo> SkillEntries
    {
        get
        {
            if (_skillEntries != null)
            {
                return _skillEntries;
            }

            _skillEntries = new();

            for (var i = 0; i < _fileIndex.IndexLength; ++i)
            {
                var info = GetSkill(i);

                if (info == null)
                {
                    break;
                }

                _skillEntries.Add(info);
            }

            return _skillEntries;
        }
        set => _skillEntries = value;
    }

    private static readonly byte[] _stringBuffer = new byte[1024];

    /// <summary>
    /// Returns <see cref="SkillInfo" /> of index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static SkillInfo GetSkill(int index)
    {
        var stream = _fileIndex.Seek(index, out var length, out var extra, out _);

        if (stream == null)
        {
            return null;
        }

        if (length == 0)
        {
            return null;
        }

        using (var bin = new BinaryReader(stream))
        {
            var action = bin.ReadBoolean();
            var name = ReadNameString(bin, length - 1);

            return new(index, name, action, extra);
        }
    }

    /// <summary>
    /// ReReads skills.mul
    /// </summary>
    public static void Reload()
    {
        _fileIndex = new("skills.idx", "skills.mul", 16);
        _skillEntries = new();

        for (var i = 0; i < _fileIndex.IndexLength; ++i)
        {
            var info = GetSkill(i);

            if (info == null)
            {
                break;
            }

            _skillEntries.Add(info);
        }
    }

    public static void Save(string path)
    {
        var idx = Path.Combine(path, "skills.idx");
        var mul = Path.Combine(path, "skills.mul");

        using var fsidx = new FileStream(idx, FileMode.Create, FileAccess.Write, FileShare.Write);

        using var fsmul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write);

        using var binidx = new BinaryWriter(fsidx);

        using var binmul = new BinaryWriter(fsmul);

        for (var i = 0; i < _fileIndex.IndexLength; ++i)
        {
            var skill = i < _skillEntries.Count ? _skillEntries[i] : null;

            if (skill == null)
            {
                binidx.Write(-1); // lookup
                binidx.Write(0);  // length
                binidx.Write(0);  // extra
            }
            else
            {
                binidx.Write((int)fsmul.Position); // lookup
                var length = (int)fsmul.Position;
                binmul.Write(skill.IsAction);

                var nameBytes = Encoding.ASCII.GetBytes(skill.Name);
                binmul.Write(nameBytes);
                binmul.Write((byte)0); // null terminated

                length = (int)fsmul.Position - length;
                binidx.Write(length);
                binidx.Write(skill.Extra);
            }
        }
    }

    private static string ReadNameString(BinaryReader bin, int length)
    {
        bin.Read(_stringBuffer, 0, length);
        int count;

        for (count = 0; count < length && _stringBuffer[count] != 0; ++count)
        {
            // TODO: this loop is weird
            //;
        }

        return Encoding.ASCII.GetString(_stringBuffer, 0, count);
    }
}
