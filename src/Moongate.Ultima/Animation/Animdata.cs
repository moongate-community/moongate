using Moongate.Ultima.Io;

namespace Moongate.Ultima.Animation;

public sealed class Animdata
{
    private static int[] _header;
    private static byte[] _unknown;

    public static Dictionary<int, AnimdataEntry> AnimData { get; set; }

    static Animdata()
    {
        Initialize();
    }

    public class AnimdataEntry
    {
        public sbyte[] FrameData { get; set; }
        public byte Unknown { get; }
        public byte FrameCount { get; set; }
        public byte FrameInterval { get; set; }
        public byte FrameStart { get; set; }

        // Empty constructor needed for deserialization.
        public AnimdataEntry()
        {
        }

        public AnimdataEntry(sbyte[] frame, byte unk, byte frameCount, byte frameInterval, byte frameStart)
        {
            FrameData = frame;
            Unknown = unk;
            FrameCount = frameCount;
            FrameInterval = frameInterval;
            FrameStart = frameStart;
        }
    }

    /// <summary>
    /// Gets Animation <see cref="AnimdataEntry" />
    /// </summary>
    /// <param name="id"></param>
    public static AnimdataEntry GetAnimData(int id)
        => AnimData.TryGetValue(id, out var value) ? value : null;

    /// <summary>
    /// Reads animdata.mul and fills <see cref="AnimData" />
    /// </summary>
    public static void Initialize()
    {
        AnimData = new();

        var path = Files.GetFilePath("animdata.mul");

        if (path == null)
        {
            return;
        }

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var bin = new BinaryReader(fs))
            {
                unsafe
                {
                    var id = 0;
                    var h = 0;

                    _header = new int[bin.BaseStream.Length / (4 + 8 * (64 + 4))];

                    while (h < _header.Length)
                    {
                        _header[h++] = bin.ReadInt32(); // chunk header

                        // Read 8 tiles
                        var buffer = bin.ReadBytes(544);

                        fixed (byte* buf = buffer)
                        {
                            var data = buf;

                            for (var i = 0; i < 8; ++i, ++id)
                            {
                                var frame = new sbyte[64];

                                for (var j = 0; j < 64; ++j)
                                {
                                    frame[j] = (sbyte)*data++;
                                }

                                var unk = *data++;
                                var frameCount = *data++;
                                var frameInterval = *data++;
                                var frameStart = *data++;

                                if (frameCount > 0)
                                {
                                    AnimData[id] = new(frame, unk, frameCount, frameInterval, frameStart);
                                }
                            }
                        }
                    }

                    var remaining = (int)(bin.BaseStream.Length - bin.BaseStream.Position);

                    if (remaining > 0)
                    {
                        _unknown = bin.ReadBytes(remaining);
                    }
                }
            }
        }
    }

    public static void Save(string path)
    {
        var fileName = Path.Combine(path, "animdata.mul");

        using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var bin = new BinaryWriter(fs))
            {
                var id = 0;
                var h = 0;
                var maxId = AnimData.Keys.Max();

                while (id <= maxId)
                {
                    var headerChunk = h < _header.Length ? _header[h++] : Random.Shared.Next();
                    bin.Write(headerChunk);

                    for (var i = 0; i < 8; ++i, ++id)
                    {
                        var animdataEntry = GetAnimData(id);

                        for (var j = 0; j < 64; ++j)
                        {
                            if (animdataEntry != null)
                            {
                                bin.Write(animdataEntry.FrameData[j]);
                            }
                            else
                            {
                                bin.Write((sbyte)0);
                            }
                        }

                        if (animdataEntry != null)
                        {
                            bin.Write(animdataEntry.Unknown);
                            bin.Write(animdataEntry.FrameCount);
                            bin.Write(animdataEntry.FrameInterval);
                            bin.Write(animdataEntry.FrameStart);
                        }
                        else
                        {
                            bin.Write((byte)0);
                            bin.Write((byte)0);
                            bin.Write((byte)0);
                            bin.Write((byte)0);
                        }
                    }
                }

                if (_unknown != null)
                {
                    bin.Write(_unknown);
                }
            }
        }
    }
}
