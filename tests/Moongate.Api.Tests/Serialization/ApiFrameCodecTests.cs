using System.Buffers.Binary;
using Moongate.Api.Data.Internal.Protocol;
using Moongate.Api.Exceptions;
using Moongate.Api.Serialization.Internal;
using Moongate.Api.Types.Protocol;

namespace Moongate.Api.Tests.Serialization;

public sealed class ApiFrameCodecTests
{
    [Fact]
    public void Encode_Request_ProducesDocumentedBytes()
    {
        var codec = new ApiFrameCodec(65536);
        var envelope = new ApiEnvelope(ApiMessageKind.Request, 42, 100, new byte[] { 0x91, 0x07 });
        Assert.Equal(Convert.FromHexString("000000099501012A64C4029107"), codec.Encode(envelope));
    }

    [Fact]
    public void Decode_GoldenFrame_CopiesPayloadAndReadsFields()
    {
        var bytes = Convert.FromHexString("000000099501012A64C4029107");
        var value = new ApiFrameCodec(9).Decode(bytes);
        bytes[^1] = 99;
        Assert.Equal(ApiMessageKind.Request, value.Kind);
        Assert.Equal(42u, value.RequestId);
        Assert.Equal((ushort)100, value.OperationId);
        Assert.Equal(new byte[] { 0x91, 0x07 }, value.Payload.ToArray());
    }

    [Theory,
     InlineData("9502012A64C4029107"), InlineData("9501042A64C4029107"),
     InlineData("9501002A64C4029107"), InlineData("9501010064C4029107"),
     InlineData("9501012A00C4029107"), InlineData("9501012A64A178"),
     InlineData("9601012A64C402910700"), InlineData("9501012A64C402910700"),
     InlineData("950101FF64C4029107"), InlineData("950101CF000000010000000064C4029107"),
     InlineData("9501012ACE00010000C4029107"), InlineData("9501012A64C0"),
     InlineData("9501012A64C600010000")]
    public void Decode_InvalidEnvelope_Rejects(string payloadHex)
    {
        var payload = Convert.FromHexString(payloadHex);
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame, 4);
        Assert.Throws<ApiProtocolException>(() => new ApiFrameCodec(65536).Decode(frame));
    }

    [Theory, InlineData(""), InlineData("00000009"), InlineData("000000099501012A64C402910700")]
    public void Decode_LengthMismatch_Rejects(string hex)
    {
        Assert.Throws<ApiProtocolException>(() => new ApiFrameCodec(65536).Decode(Convert.FromHexString(hex)));
    }

    [Fact]
    public void Encode_EnvelopeExceedsLimit_Rejects()
    {
        Assert.Throws<ApiProtocolException>(() => new ApiFrameCodec(8).Encode(
                new ApiEnvelope(ApiMessageKind.Request, 42, 100, new byte[] { 0x91, 0x07 })
            )
        );
    }
}
