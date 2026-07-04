using Moongate.Ultima.Io;

namespace Moongate.Ultima.Animation;

public sealed class BodyTable
{
    public static Dictionary<int, BodyTableEntry> Entries { get; private set; }

    static BodyTable()
    {
        Initialize();
    }

    public static void Initialize()
    {
        Entries = new();

        var filePath = Files.GetFilePath("body.def");

        if (filePath == null)
        {
            return;
        }

        using (var def = new StreamReader(filePath))
        {
            while (def.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                try
                {
                    var index1 = line.IndexOf('{');
                    var index2 = line.IndexOf('}');

                    var param1 = line.Substring(0, index1);
                    var param2 = line.Substring(index1 + 1, index2 - index1 - 1);
                    var param3 = line.Substring(index2 + 1);

                    var indexOf = param2.IndexOf(',');

                    if (indexOf > -1)
                    {
                        param2 = param2.Substring(0, indexOf).Trim();
                    }

                    var iParam1 = Convert.ToInt32(param1.Trim());
                    var iParam2 = Convert.ToInt32(param2.Trim());
                    var iParam3 = Convert.ToInt32(param3.Trim());

                    Entries[iParam1] = new(iParam2, iParam1, iParam3);
                }
                catch
                {
                    // TODO: ignored?
                    // ignored
                }
            }
        }
    }
}
