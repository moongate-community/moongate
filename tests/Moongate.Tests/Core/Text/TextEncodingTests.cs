using System.Text;
using Moongate.Core.Text;

namespace Moongate.Tests.Core.Text;

public class TextEncodingTests
{
    [Theory]
    [InlineData(16, false)]
    [InlineData(16, true)]
    [InlineData(512, false)]
    [InlineData(512, true)]
    public void GetString_StackAndPooledPaths_PreserveSafeFiltering(int prefixLength, bool safeString)
    {
        var prefix = new string('a', prefixLength);
        var input = prefix + "\u0001caffè";
        var bytes = Encoding.UTF8.GetBytes(input);

        var result = TextEncoding.GetString(bytes, Encoding.UTF8, safeString);

        Assert.Equal(safeString ? prefix + "caffè" : input, result);
    }
}
