using Moongate.Http.Plugin.Services.Assets;
using Moongate.Http.Plugin.Types;
using Xunit;

namespace Moongate.Tests.Http.Services.Assets;

public sealed class AssetUploadValidationTests
{
    [Theory]
    [InlineData("image/png", "png")]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("image/webp", "webp")]
    [InlineData("image/svg+xml", "svg")]
    [InlineData("image/x-icon", "ico")]
    public void Validate_AcceptsWhitelistedTypes(string contentType, string extension)
    {
        var result = AssetUploadValidation.Validate(contentType, 1024, 2_097_152);

        Assert.True(result.Ok);
        Assert.Equal(extension, result.Extension);
    }

    [Fact]
    public void Validate_RejectsUnknownType()
    {
        var result = AssetUploadValidation.Validate("application/zip", 10, 2_097_152);

        Assert.False(result.Ok);
        Assert.Equal(AssetValidationError.UnsupportedType, result.Error);
    }

    [Fact]
    public void Validate_RejectsTooLarge()
    {
        var result = AssetUploadValidation.Validate("image/png", 5_000, 4_096);

        Assert.False(result.Ok);
        Assert.Equal(AssetValidationError.TooLarge, result.Error);
    }
}
