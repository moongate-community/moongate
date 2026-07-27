using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Server;
using Moongate.Tests.Support;
using Xunit;

namespace Moongate.Tests.Server.Services.Server;

public sealed class ServerSettingsServiceTests
{
    private static ServerSettingsService Create()
        => new(new FakePersistenceService());

    [Fact]
    public void Get_FirstAccess_CreatesDisabledDefault()
    {
        var service = Create();

        var settings = service.Get();

        Assert.False(settings.RegistrationEnabled);
        Assert.Empty(settings.Assets);
    }

    [Fact]
    public void Get_IsStableAcrossCalls()
    {
        var service = Create();

        var first = service.Get();
        var second = service.Get();

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void Update_NullFields_LeaveExistingUntouched()
    {
        var service = Create();
        service.Update(new() { Description = "First", RegistrationEnabled = true });

        service.Update(new() { RegistrationEnabled = null, Description = null });

        var settings = service.Get();
        Assert.Equal("First", settings.Description);
        Assert.True(settings.RegistrationEnabled);
    }

    [Fact]
    public void Update_Tagline_RoundTrips()
    {
        var service = Create();

        service.Update(new() { Tagline = "Sosaria never sleeps." });

        Assert.Equal("Sosaria never sleeps.", service.Get().Tagline);
    }

    [Fact]
    public void SetAsset_ThenClear_RoundTrips()
    {
        var service = Create();

        service.SetAsset(ServerAssetSlotType.Logo, new() { FileName = "logo.png", ContentType = "image/png" });
        Assert.Equal("logo.png", service.Get().Assets[nameof(ServerAssetSlotType.Logo)].FileName);

        service.ClearAsset(ServerAssetSlotType.Logo);
        Assert.DoesNotContain(nameof(ServerAssetSlotType.Logo), service.Get().Assets.Keys);
    }
}
