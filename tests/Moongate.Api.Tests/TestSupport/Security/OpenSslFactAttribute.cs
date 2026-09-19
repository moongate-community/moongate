namespace Moongate.Api.Tests.TestSupport.Security;

/// <summary>A fact that is skipped when the openssl binary is not on the PATH.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class OpenSslFactAttribute : FactAttribute
{
    public OpenSslFactAttribute()
    {
        if (!ScriptedCertificates.IsOpenSslAvailable())
        {
            Skip = "openssl is not on the PATH";
        }
    }
}
