using System;
using System.IO;
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

        char[] buffer = new char[length];
        reader.Read(buffer, 0, length);
        return new string(buffer);
    }

    public static void WriteString(this BinaryWriter writer, string data)
    {
        ArgumentNullException.ThrowIfNull(writer);

        ArgumentNullException.ThrowIfNull(data);

        byte[] bytes = Encoding.ASCII.GetBytes(data);
        writer.Write(bytes);
    }
}
