namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AdminProtocolFactAttribute : FactAttribute
{
    public AdminProtocolFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("MOONGATE_ADMIN_PYTHON")))
        {
            Skip = "Run scripts/verify-admin-protos.sh to provision the isolated Python compiler/client.";
        }
    }
}
