using System.Text;

namespace Moongate.Ultima.Io;

public static class BinaryExtensions
{
    public static string ReadString(this BinaryReader reader, int length)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (length < 0 || length > reader.BaseStream.Length + reader.BaseStream.Position)
        {
            throw new ArgumentException("Out of range.");
        }

        var buffer = new char[length];
        reader.Read(buffer, 0, length);

        return new(buffer);
    }

    public static void WriteString(this BinaryWriter writer, string data)
    {
        ArgumentNullException.ThrowIfNull(writer);

        ArgumentNullException.ThrowIfNull(data);

        var bytes = Encoding.ASCII.GetBytes(data);
        writer.Write(bytes);
    }
}
