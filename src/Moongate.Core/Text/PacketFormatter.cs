/*************************************************************************
 * ModernUO                                                              *
 * Copyright 2019-2023 - ModernUO Development Team                       *
 * Email: hi@modernuo.com                                                *
 * File: Utility.cs                                                      *
 *                                                                       *
 * This program is free software: you can redistribute it and/or modify  *
 * it under the terms of the GNU General Public License as published by  *
 * the Free Software Foundation, either version 3 of the License, or     *
 * (at your option) any later version.                                   *
 *                                                                       *
 * You should have received a copy of the GNU General Public License     *
 * along with this program.  If not, see <http://www.gnu.org/licenses/>. *
 *************************************************************************/

using Moongate.Core.Buffers;

namespace Moongate.Core.Text;

/// <summary>Renders a raw packet buffer as a readable hex dump.</summary>
/// <remarks>
/// The dump layout is derived from ModernUO's <c>Utility.FormatBuffer</c>; the opcode header is an addition.
/// </remarks>
public static class PacketFormatter
{
    private const int BytesPerLine = 16;

    private const string ColumnHeader = "        0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F";
    private const string ColumnRule = "       -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- --";

    /// <summary>Formats a packet as an opcode header followed by an offset-addressed hex dump.</summary>
    /// <param name="data">The complete packet buffer, whose first byte is the opcode.</param>
    /// <returns>The formatted dump, using <c>\n</c> line endings on every platform.</returns>
    public static string Format(ReadOnlySpan<byte> data)
    {
        // Capacity covers the header plus one 54-character line per 16 bytes; the builder grows if it is short.
        var builder = ValueStringBuilder.Create(64 + (data.Length / BytesPerLine + 1) * 56);

        try
        {
            AppendHeader(ref builder, data);

            if (data.Length == 0)
            {
                // A grid with no rows under it is noise, so an empty buffer stops at the header.
                return builder.ToString();
            }

            builder.Append('\n');
            builder.Append(ColumnHeader);
            builder.Append('\n');
            builder.Append(ColumnRule);
            builder.Append('\n');

            for (var offset = 0; offset < data.Length; offset += BytesPerLine)
            {
                var length = Math.Min(data.Length - offset, BytesPerLine);

                AppendLine(ref builder, data.Slice(offset, length), offset);
            }

            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void AppendHeader(ref ValueStringBuilder builder, ReadOnlySpan<byte> data)
    {
        builder.Append("Opcode: ");

        if (data.IsEmpty)
        {
            builder.Append("(none)");
        }
        else
        {
            builder.Append("0x");
            builder.Append(data[0], "X2");
        }

        builder.Append("  Length: ");
        builder.Append(data.Length);
        builder.Append('\n');
    }

    private static void AppendLine(ref ValueStringBuilder builder, ReadOnlySpan<byte> line, int offset)
    {
        builder.Append(offset, "X4");
        builder.Append(' ', 3);

        for (var i = 0; i < line.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line[i], "X2");
        }

        builder.Append('\n');
    }
}
