using MailKit.Security;
using Moongate.Smtp.Plugin.Extensions;
using Moongate.Smtp.Plugin.Types;

namespace Moongate.Tests.Smtp;

public sealed class SmtpSecurityTypeExtensionsTests
{
    [Theory]
    [InlineData(SmtpSecurityType.None, SecureSocketOptions.None)]
    [InlineData(SmtpSecurityType.StartTls, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurityType.SslOnConnect, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpSecurityType.Auto, SecureSocketOptions.Auto)]
    public void ToSocketOptions_MapsEveryDeclaredValue(SmtpSecurityType security, SecureSocketOptions expected)
        => Assert.Equal(expected, security.ToSocketOptions());

    [Fact]
    public void ToSocketOptions_EveryEnumValueIsCovered()
        // The mapping falls through to Auto, so a value added later would silently pick it up rather
        // than being noticed. This fails the moment the enum grows without the theory above growing too.
        => Assert.Equal(4, Enum.GetValues<SmtpSecurityType>().Length);

    [Fact]
    public void ToSocketOptions_AnUnrecognisedValue_FallsBackToAuto()
        => Assert.Equal(SecureSocketOptions.Auto, ((SmtpSecurityType)99).ToSocketOptions());
}
