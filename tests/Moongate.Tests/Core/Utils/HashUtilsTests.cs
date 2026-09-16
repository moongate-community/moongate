using Moongate.Core.Utils;

namespace Moongate.Tests.Core.Utils;

public sealed class HashUtilsTests
{
    [Theory, InlineData(null), InlineData(""), InlineData(" \t")]
    public void HashPassword_RejectsMissingPassword(string? password)
    {
        Assert.Throws<ArgumentException>(() => HashUtils.HashPassword(password!));
    }

    [Fact]
    public void HashPassword_ProducesSaltedPayloadThatVerifiesOnlyMatchingPassword()
    {
        var first = HashUtils.HashPassword("synthetic-test-password");
        var second = HashUtils.HashPassword("synthetic-test-password");

        Assert.NotEqual(first, second);
        Assert.True(HashUtils.VerifyPassword("synthetic-test-password", first));
        Assert.True(HashUtils.VerifyPassword("synthetic-test-password", second));
        Assert.False(HashUtils.VerifyPassword("different-test-password", first));
    }

    // Independent fixtures generated with Python hashlib.pbkdf2_hmac("sha256", ...).
    [Theory,
     InlineData("test-password", "pbkdf2-sha256$1$c2FsdA==$BiPkvtz+xs0uiXGY/ePDdA8vCrlbC64ObpUq6FQ03o0="),
     InlineData("pássword", "pbkdf2-sha256$2$bW9vbmdhdGUtZml4dHVyZQ==$/TvyHf0N4okxaintqzv78UA6bXlqID6JqoREho/xMHI=")]
    public void VerifyPassword_AcceptsIndependentPbkdf2Fixtures(string password, string payload)
    {
        Assert.True(HashUtils.VerifyPassword(password, payload));
        Assert.False(HashUtils.VerifyPassword(password + "!", payload));
    }

    [Theory,
     InlineData(null, "payload"),
     InlineData("", "payload"),
     InlineData(" \t", "payload"),
     InlineData("test-password", null),
     InlineData("test-password", ""),
     InlineData("test-password", " ")]
    public void VerifyPassword_RejectsMissingInputs(string? password, string? payload)
    {
        Assert.False(HashUtils.VerifyPassword(password!, payload!));
    }

    [Theory,
     InlineData("invalid"),
     InlineData("pbkdf2-sha256$1$c2FsdA=="),
     InlineData("pbkdf2-sha256$1$c2FsdA==$AA==$extra"),
     InlineData("sha256$1$c2FsdA==$AA=="),
     InlineData("PBKDF2-SHA256$1$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$0$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$-1$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$+1$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$ 1$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$abc$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$2147483648$c2FsdA==$AA=="),
     InlineData("pbkdf2-sha256$1$not base64!$AA=="),
     InlineData("pbkdf2-sha256$1$c2FsdA==$not base64!"),
     InlineData("pbkdf2-sha256$1$c2FsdA==$")]
    public void VerifyPassword_RejectsMalformedPayload(string payload)
    {
        Assert.False(HashUtils.VerifyPassword("test-password", payload));
    }
}
