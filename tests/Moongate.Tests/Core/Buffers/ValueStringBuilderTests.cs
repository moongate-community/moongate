using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Moongate.Core.Buffers;
using Moongate.Tests.TestSupport.Buffers;

namespace Moongate.Tests.Core.Buffers;

public class ValueStringBuilderTests
{
    [Fact]
    public void AppendLine_MultiCharacterAndEmptyText_AppendsOneLineEndingEach()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());

        builder.AppendLine("first");
        builder.AppendLine("");

        Assert.Equal("first" + Environment.NewLine + Environment.NewLine, builder.ToString());
    }

    [Fact]
    public void AppendSpan_ExceedsCapacity_ProvidesWritableSpaceAfterExistingText()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("ab");

        "cdef".AsSpan().CopyTo(builder.AppendSpan(4));

        Assert.Equal("abcdef", builder.ToString());
    }

    [Fact]
    public void AppendSpan_NegativeLength_ThrowsWithoutChangingText()
    {
        using var builder = new ValueStringBuilder(new char[8].AsSpan());
        builder.Append("prefix");
        Exception? exception = null;

        try
        {
            builder.AppendSpan(-1);
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("prefix", builder.ToString());
    }

    [Fact]
    public void Append_GenericFormattableWithoutSpanSupport_ForwardsFormat()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        var value = new FallbackFormattable();

        builder.Append(value, "requested");

        Assert.Equal("formatted value", builder.ToString());
        Assert.Equal("requested", value.ReceivedFormat);
        Assert.Null(value.ReceivedProvider);
    }

    [Fact]
    public void Append_GenericObjectAndNull_UsesTextAndSkipsNull()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());

        builder.Append(new StringBuilder("plain text"));
        builder.Append<object?>(null);
        builder.Append(null);

        Assert.Equal("plain text", builder.ToString());
    }

    [Fact]
    public void Append_GenericSpanFormattable_GrowsAndAppliesFormat()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("#");

        builder.Append(42, "D6");

        Assert.Equal("#000042", builder.ToString());
    }

    [Fact]
    public void Append_InterpolationWithProvider_UsesRequestedCulture()
    {
        using var builder = ValueStringBuilder.Create();

        builder.Append(formatProvider: CultureInfo.GetCultureInfo("it-IT"), $"price={12.5m:F2}");
        builder.Append(formatProvider: CultureInfo.InvariantCulture, $"/{12.5m:F2}");

        Assert.Equal("price=12,50/12.50", builder.ToString());
    }

    [Fact]
    public void Append_Interpolation_PreservesFormatsAlignmentSpansAndNulls()
    {
        using var builder = new ValueStringBuilder(stackalloc char[4]);
        string? missing = null;

        builder.Append($"id={42:D4} [{"hello".AsSpan(1, 3),5}] /{missing}");

        Assert.Equal("id=0042 [  ell] /", builder.ToString());
    }

    [Fact]
    public void Append_LargeInterpolation_PreservesTextAcrossHandlerAndBuilderGrowth()
    {
        using var builder = ValueStringBuilder.Create(1);
        var text = new string('x', 4096);

        builder.Append($"<{text}>/{42:D4}");

        Assert.Equal("<" + text + ">/0042", builder.ToString());
    }

    [Fact]
    public void Append_NativeHandler_CopiesFormattedTextIntoBuilder()
    {
        using var builder = ValueStringBuilder.CreateMT(1);
        var handler = new DefaultInterpolatedStringHandler(0, 1, CultureInfo.InvariantCulture);
        handler.AppendLiteral("value:");
        handler.AppendFormatted(42, "D4");

        builder.Append(handler);
        builder.Append("!");

        Assert.Equal("value:0042!", builder.ToString());
    }

    [Fact]
    public void Append_RepeatedCharacter_GrowsAndSupportsZeroCount()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("ab");

        builder.Append('x', 5);
        builder.Append('y', 0);

        Assert.Equal("abxxxxx", builder.ToString());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Append_SpanFormatterReportsInvalidCount_ThrowsWithoutAdvancingLength(bool negativeCount)
    {
        Assert.Throws<FormatException>(() =>
            {
                using var builder = new ValueStringBuilder(new char[16].AsSpan());
                builder.Append("prefix");

                try
                {
                    builder.Append(new InvalidSpanFormattable(negativeCount));
                }
                finally
                {
                    Assert.Equal("prefix", builder.ToString());
                }
            }
        );
    }

    [Fact]
    public void Append_StackBufferGrows_PreservesWrittenCharactersAndEdits()
    {
        using var builder = new ValueStringBuilder(stackalloc char[4]);
        builder.Append("ab");
        "cd".AsSpan().CopyTo(builder.AppendSpan(2));
        builder.Append("efghijklmnopqrstuvwxyz");
        builder.Insert(2, "-");
        builder.Remove(2, 1);

        Assert.Equal("abcdefghijklmnopqrstuvwxyz", builder.ToString());
        Assert.Equal(26, builder.Length);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", builder.AsSpan().ToString());
    }

    [Fact]
    public void AsSpan_FromStart_ExcludesUnusedCapacity()
    {
        using var builder = new ValueStringBuilder(new char[16].AsSpan());
        builder.Append("abcdef");

        Assert.Equal("cdef", builder.AsSpan(2).ToString());
        Assert.True(builder.AsSpan(6).IsEmpty);
    }

    [Fact]
    public void AsSpan_Subrange_ReturnsRequestedWrittenCharacters()
    {
        using var builder = new ValueStringBuilder(new char[16].AsSpan());
        builder.Append("abcdef");

        Assert.Equal("bcd", builder.AsSpan(1, 3).ToString());
        Assert.Equal("", builder.AsSpan(6, 0).ToString());
    }

    [Fact]
    public void AsSpan_Termination_GrowsFullBufferAndLeavesTextLengthUnchanged()
    {
        using var builder = new ValueStringBuilder(new char[3].AsSpan());
        builder.Append("abc");

        Assert.Equal("abc", builder.AsSpan(false).ToString());
        Assert.Equal("abc", builder.AsSpan(true).ToString());
        Assert.Equal('\0', builder.RawChars[3]);
        Assert.Equal(3, builder.Length);
        builder.Append("d");
        Assert.Equal("abcd", builder.ToString());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Constructor_InitialTextWithOrWithoutSuppliedBuffer_CopiesTextBeforeAppending(bool suppliedBuffer)
    {
        var source = "seed".ToCharArray();
        using var builder = suppliedBuffer
            ? new ValueStringBuilder(source.AsSpan(), new char[2])
            : new ValueStringBuilder((ReadOnlySpan<char>)source);
        source[0] = 'X';

        builder.Append("-next");

        Assert.Equal("seed-next", builder.ToString());
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Create_BothPoolModes_PreserveCopyResetAndNullAppendBehavior(bool mt)
    {
        using var builder = ValueStringBuilder.Create(1, mt);
        builder.Append("a");
        builder.AppendLine(null);
        builder.AppendLine("b");
        var expected = "ab" + Environment.NewLine;

        Assert.Equal(expected, builder.ToString());
        Assert.Equal(expected, builder.ToString());
        Assert.False(builder.TryCopyTo(new char[1], out var tooSmall));
        Assert.Equal(0, tooSmall);
        Span<char> destination = new char[16];
        Assert.True(builder.TryCopyTo(destination, out var written));
        Assert.Equal(expected, destination[..written].ToString());

        builder.Reset();
        builder.Append('x', 2);
        Assert.Equal("xx", builder.ToString());
    }

    [Fact]
    public void Dispose_DefaultAndRentedBuilder_CanBeRepeated()
    {
        var builder = default(ValueStringBuilder);
        builder.Dispose();
        builder.Append("hello");
        Assert.Equal("hello", builder.ToString());

        builder.Dispose();
        builder.Dispose();

        Assert.Equal(0, builder.Length);
    }

    [Fact]
    public void EnsureCapacity_GrowsAndKeepsTextAvailableForFurtherEdits()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("ab");

        builder.EnsureCapacity(20);
        var capacity = builder.Capacity;
        builder.EnsureCapacity(1);
        builder[1] = 'B';
        builder.Append("cd");

        Assert.True(capacity >= 20);
        Assert.Equal(capacity, builder.Capacity);
        Assert.Equal("aBcd", builder.ToString());
    }

    [Fact]
    public void GetPinnableReference_AllOverloads_ExposeFirstCharacterAndHonorTermination()
    {
        using var builder = new ValueStringBuilder(new char[3].AsSpan());
        builder.Append("abc");

        builder.GetPinnableReference() = 'A';
        builder.GetPinnableReference(false) = 'B';
        builder.GetPinnableReference(true) = 'C';

        Assert.Equal("Cbc", builder.ToString());
        Assert.Equal('\0', builder.RawChars[3]);
    }

    [Fact]
    public void Insert_CharactersAtStartMiddleAndEnd_ShiftsExistingTextAcrossGrowth()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("ab");

        builder.Insert(1, '-', 3);
        builder.Insert(0, '<', 1);
        builder.Insert(builder.Length, '>', 1);
        builder.Insert(2, '?', 0);

        Assert.Equal("<a---b>", builder.ToString());
    }

    [Fact]
    public void Insert_StringAtStartMiddleAndEnd_GrowsAndIgnoresNull()
    {
        using var builder = new ValueStringBuilder(new char[2].AsSpan());
        builder.Append("ab");

        builder.Insert(1, "middle");
        builder.Insert(0, "<");
        builder.Insert(builder.Length, ">");
        builder.Insert(2, null);

        Assert.Equal("<amiddleb>", builder.ToString());
    }

    [Theory,
     InlineData(-1, 1, "startIndex"),
     InlineData(0, -1, "length"),
     InlineData(4, 3, "length"),
     InlineData(7, 0, "length")]
    public void Remove_InvalidRange_ThrowsWithoutChangingText(int start, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                using var builder = new ValueStringBuilder(new char[8].AsSpan());
                builder.Append("abcdef");

                try
                {
                    builder.Remove(start, count);
                }
                finally
                {
                    Assert.Equal("abcdef", builder.ToString());
                }
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Theory,
     InlineData(0, 2, "cdef!"),
     InlineData(4, 2, "abcd!"),
     InlineData(2, 2, "abef!"),
     InlineData(0, 6, "!"),
     InlineData(6, 0, "abcdef!")]
    public void Remove_SelectedRange_PreservesRemainingTextAndSupportsAppending(int start, int count, string expected)
    {
        using var builder = new ValueStringBuilder(new char[8].AsSpan());
        builder.Append("abcdef");

        builder.Remove(start, count);
        builder.Append("!");

        Assert.Equal(expected, builder.ToString());
    }

    [Fact]
    public void ReplaceAny_EmptyRangeAndMissingCharacters_LeaveTextAndUnusedStorageUnchanged()
    {
        var storage = "xxxxxxxx".ToCharArray();
        using var builder = new ValueStringBuilder(storage.AsSpan());
        builder.Append("abca");

        builder.ReplaceAny("ab", "XY", 2, 0);
        builder.ReplaceAny("xy", "YZ", 0, 4);

        Assert.Equal("abcaxxxx", new(storage));
        Assert.Equal("abca", builder.ToString());
    }

    [Theory,
     InlineData(-1, 1, "startIndex"),
     InlineData(7, 0, "startIndex"),
     InlineData(0, -1, "count"),
     InlineData(4, 3, "count")]
    public void ReplaceAny_InvalidRange_ThrowsWithoutChangingText(int start, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                using var builder = new ValueStringBuilder(new char[8].AsSpan());
                builder.Append("abcdef");

                try
                {
                    builder.ReplaceAny("ab", "XY", start, count);
                }
                finally
                {
                    Assert.Equal("abcdef", builder.ToString());
                }
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void ReplaceAny_MapsCharactersOnlyWithinRequestedRange()
    {
        using var builder = new ValueStringBuilder(new char[8].AsSpan());
        builder.Append("abacba");

        builder.ReplaceAny("ab", "XY", 2, 3);

        Assert.Equal("abXcYa", builder.ToString());
    }

    [Fact]
    public void Replace_ChangesOnlyMatchesWithinRequestedRange()
    {
        using var builder = new ValueStringBuilder(new char[8].AsSpan());
        builder.Append("abacad");

        builder.Replace('a', 'X', 2, 2);

        Assert.Equal("abXcad", builder.ToString());
    }

    [Fact]
    public void Replace_EmptyRangeAndMissingCharacter_LeaveTextAndUnusedStorageUnchanged()
    {
        var storage = "xxxxxxxx".ToCharArray();
        using var builder = new ValueStringBuilder(storage.AsSpan());
        builder.Append("abca");

        builder.Replace('a', 'X', 2, 0);
        builder.Replace('x', 'Y', 0, 4);

        Assert.Equal("abcaxxxx", new(storage));
        Assert.Equal("abca", builder.ToString());
    }

    [Theory,
     InlineData(-1, 1, "startIndex"),
     InlineData(7, 0, "startIndex"),
     InlineData(0, -1, "count"),
     InlineData(4, 3, "count")]
    public void Replace_InvalidRange_ThrowsWithoutChangingText(int start, int count, string parameter)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                using var builder = new ValueStringBuilder(new char[8].AsSpan());
                builder.Append("abcdef");

                try
                {
                    builder.Replace('a', 'X', start, count);
                }
                finally
                {
                    Assert.Equal("abcdef", builder.ToString());
                }
            }
        );

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void TryCopyTo_SuccessAndFailure_PreserveDestinationOutsideWrittenCharacters()
    {
        using var builder = new ValueStringBuilder(new char[8].AsSpan());
        builder.Append("hello");
        var small = "xxxx".ToCharArray();
        var large = "xxxxxxxx".ToCharArray();

        Assert.False(builder.TryCopyTo(small, out var failedCount));
        Assert.Equal(0, failedCount);
        Assert.Equal("xxxx", new(small));
        Assert.True(builder.TryCopyTo(large, out var written));
        Assert.Equal(5, written);
        Assert.Equal("helloxxx", new(large));
        Assert.Equal("hello", builder.ToString());
    }
}
