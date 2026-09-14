using System.Globalization;
using System.Runtime.CompilerServices;
using Moongate.Core.Buffers;

namespace Moongate.Tests.Core.Buffers;

public class ValueStringBuilderTests
{
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
    public void Append_Interpolation_PreservesFormatsAlignmentSpansAndNulls()
    {
        using var builder = new ValueStringBuilder(stackalloc char[4]);
        string? missing = null;

        builder.Append($"id={42:D4} [{"hello".AsSpan(1, 3),5}] /{missing}");

        Assert.Equal("id=0042 [  ell] /", builder.ToString());
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
    public void Append_LargeInterpolation_PreservesTextAcrossHandlerAndBuilderGrowth()
    {
        using var builder = ValueStringBuilder.Create(1);
        var text = new string('x', 4096);

        builder.Append($"<{text}>/{42:D4}");

        Assert.Equal("<" + text + ">/0042", builder.ToString());
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
}
