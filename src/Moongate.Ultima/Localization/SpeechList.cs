using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Localization;

public sealed class SpeechList
{
    public static List<SpeechEntry> Entries { get; private set; }

    private static readonly byte[] _buffer = new byte[128];

    static SpeechList()
    {
        Initialize();
    }

    public class IdComparer : IComparer<SpeechEntry>
    {
        private readonly bool _sortDescending;

        public IdComparer(bool sortDescending)
        {
            _sortDescending = sortDescending;
        }

        public int Compare(SpeechEntry x, SpeechEntry y)
        {
            if (x.Id == y.Id)
            {
                return 0;
            }

            if (_sortDescending)
            {
                return x.Id < y.Id ? 1 : -1;
            }

            return x.Id < y.Id ? -1 : 1;
        }
    }

    public class KeyWordComparer : IComparer<SpeechEntry>
    {
        private readonly bool _sortDescending;

        public KeyWordComparer(bool sortDescending)
        {
            _sortDescending = sortDescending;
        }

        public int Compare(SpeechEntry x, SpeechEntry y)
        {
            if (_sortDescending)
            {
                return string.CompareOrdinal(y.KeyWord, x.KeyWord);
            }

            return string.CompareOrdinal(x.KeyWord, y.KeyWord);
        }
    }

    private sealed class OrderComparer : IComparer<SpeechEntry>
    {
        public int Compare(SpeechEntry x, SpeechEntry y)
        {
            if (x.Order == y.Order)
            {
                return 0;
            }

            return x.Order < y.Order ? -1 : 1;
        }
    }

    public static void ExportToCsv(string fileName)
    {
        using (var tex = new StreamWriter(new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite), Encoding.Unicode))
        {
            tex.WriteLine("Order;ID;KeyWord");

            foreach (var entry in Entries)
            {
                tex.WriteLine($"{entry.Order};{entry.Id};{entry.KeyWord}");
            }
        }
    }

    public static void ImportFromCsv(string fileName)
    {
        Entries = new(0);

        if (!File.Exists(fileName))
        {
            return;
        }

        using (var sr = new StreamReader(fileName))
        {
            while (sr.ReadLine() is { } line)
            {
                line = line.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                if (line.Contains("Order") && line.Contains("KeyWord"))
                {
                    continue;
                }

                try
                {
                    var split = line.Split(';');

                    if (split.Length < 3)
                    {
                        continue;
                    }

                    var order = ConvertStringToInt(split[0]);
                    var id = ConvertStringToInt(split[1]);
                    var word = split[2];
                    word = word.Replace("\"", "");
                    Entries.Add(new((short)id, word, order));
                }
                catch
                {
                    // TODO: ignored?
                    // ignored
                }
            }
        }
    }

    /// <summary>
    /// Loads speech.mul in <see cref="SpeechList.Entries" />
    /// </summary>
    public static void Initialize()
    {
        var path = Files.GetFilePath("speech.mul");

        if (path == null)
        {
            Entries = new(0);

            return;
        }

        const int capacity = 6500; // speech.mul contains around 6500 entries so we can start with this value

        Entries = new(capacity);

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var bin = new BinaryReader(fs))
            {
                var order = 0;

                while (bin.BaseStream.Length != bin.BaseStream.Position)
                {
                    var id = BinaryPrimitives.ReverseEndianness(bin.ReadInt16());
                    var length = BinaryPrimitives.ReverseEndianness(bin.ReadInt16());

                    if (length > 128)
                    {
                        length = 128;
                    }

                    _ = bin.Read(_buffer, 0, length);

                    var keyword = Encoding.UTF8.GetString(_buffer, 0, length);

                    Entries.Add(new(id, keyword, order));

                    ++order;
                }
            }
        }
    }

    /// <summary>
    /// Saves speech.mul to <paramref name="fileName" />
    /// </summary>
    /// <param name="fileName"></param>
    public static void SaveSpeechList(string fileName)
    {
        Entries.Sort(new OrderComparer());

        using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var bin = new BinaryWriter(fs))
            {
                foreach (var entry in Entries)
                {
                    bin.Write(BinaryPrimitives.ReverseEndianness(entry.Id));
                    var utf8String = Encoding.UTF8.GetBytes(entry.KeyWord);
                    var length = (short)utf8String.Length;
                    bin.Write(BinaryPrimitives.ReverseEndianness(length));
                    bin.Write(utf8String);
                }
            }
        }
    }

    private static int ConvertStringToInt(string text)
    {
        int result;

        if (text.Contains("0x"))
        {
            var convert = text.Replace("0x", "");
            int.TryParse(convert, NumberStyles.HexNumber, null, out result);
        }
        else
        {
            int.TryParse(text, NumberStyles.Integer, null, out result);
        }

        return result;
    }
}
