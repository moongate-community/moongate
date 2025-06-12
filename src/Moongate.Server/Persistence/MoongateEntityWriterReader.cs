using System.IO.Compression;
using System.Text;
using Moongate.Core.Json;
using Moongate.Core.Persistence.Interfaces.Entities;

namespace Moongate.Server.Persistence;

public class MoongateEntityWriterReader : IEntityReader, IEntityWriter
{
    public TEntity DeserializeEntity<TEntity>(byte[] data, Type entityType) where TEntity : class
    {
        if (data.Length == 0)
        {
            return null;
        }

        var decompressedData = Decompress(data);
        var jsonString = Encoding.UTF8.GetString(decompressedData);

        return JsonUtils.Deserialize<TEntity>(jsonString);
    }

    public byte[] SerializeEntity<T>(T entity) where T : class
    {
        var jsonArray = Encoding.UTF8.GetBytes(JsonUtils.Serialize(entity));

        return Compress(jsonArray);
    }

    public static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionMode.Compress, true))
        {
            brotli.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    public static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}
