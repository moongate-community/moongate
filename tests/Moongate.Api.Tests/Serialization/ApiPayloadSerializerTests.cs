using MessagePack;
using Moongate.Api.Exceptions;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Tests.TestSupport.Contracts;
namespace Moongate.Api.Tests.Serialization;
public sealed class ApiPayloadSerializerTests
{
    [Fact]
    public void Deserialize_AppendedField_AcceptsCompatibleSchema()
    {
        var value = ApiPayloadSerializer.Deserialize<IncrementRequest>(Convert.FromHexString("9207A3666F6F"));
        Assert.Equal(7, value.Value);
        var other = ApiPayloadSerializer.Deserialize<AlternativeRequest>(Convert.FromHexString("9107"));
        Assert.Equal(7, other.Value);
    }

    [Theory, InlineData("910700"), InlineData("91A178"), InlineData("C0"), InlineData("81A17801"), InlineData("D40000")]
    public void Deserialize_MalformedOrUnsupportedValue_Rejects(string hex)
    {
        Assert.ThrowsAny<MessagePackSerializationException>(() => ApiPayloadSerializer.Deserialize<IncrementRequest>(Convert.FromHexString(hex)));
    }

    [Theory, InlineData("DDFFFFFFFF"), InlineData("DBFFFFFFFF"), InlineData("C6FFFFFFFF")]
    public void Deserialize_HostileLength_RejectsBeforeAllocation(string hex)
    {
        Assert.Throws<ApiProtocolException>(() => ApiPayloadSerializer.Deserialize<IncrementRequest>(Convert.FromHexString(hex)));
    }

    [Fact]
    public void Deserialize_ExcessiveDepth_Rejects()
    {
        var input = Enumerable.Repeat((byte)0x91, 65).Append((byte)0xC0).ToArray();
        Assert.Throws<ApiProtocolException>(() => ApiPayloadSerializer.Deserialize<IncrementRequest>(input));
    }

    [Fact]
    public void Serialize_OversizedValue_Rejects()
    {
        Assert.Throws<ApiProtocolException>(() => ApiPayloadSerializer.Serialize(new byte[1000000], 65536));
    }

    [Fact]
    public void Serialize_LargeValidAscii_AllowsEncodedSizeWithinLimit()
    {
        var value = new string('a', 60000);
        var encoded = ApiPayloadSerializer.Serialize(value, 65536);
        Assert.Equal(60003, encoded.Length);
        Assert.Equal(value, ApiPayloadSerializer.Deserialize<string>(encoded));
    }
}
