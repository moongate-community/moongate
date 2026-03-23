using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Modules;

public sealed class HealingModuleTests
{
    private sealed class RecordingBandageService : IBandageService
    {
        public Serial LastBeginMobileId { get; private set; }
        public Serial LastHasBandageMobileId { get; private set; }
        public Serial LastIsBandagingMobileId { get; private set; }
        public bool BeginResult { get; set; }
        public bool HasBandageResult { get; set; }
        public bool IsBandagingResult { get; set; }

        public Task<bool> BeginSelfBandageAsync(Serial mobileId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastBeginMobileId = mobileId;

            return Task.FromResult(BeginResult);
        }

        public Task<bool> HasBandageAsync(Serial mobileId, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            LastHasBandageMobileId = mobileId;

            return Task.FromResult(HasBandageResult);
        }

        public bool IsBandaging(Serial mobileId)
        {
            LastIsBandagingMobileId = mobileId;

            return IsBandagingResult;
        }
    }

    [Test]
    public void Methods_ShouldDelegateToBandageService()
    {
        var bandageService = new RecordingBandageService
        {
            BeginResult = true,
            HasBandageResult = true,
            IsBandagingResult = true
        };
        var module = new HealingModule(bandageService);

        var began = module.BeginSelfBandage(0x401);
        var hasBandage = module.HasBandage(0x401);
        var isBandaging = module.IsBandaging(0x401);

        Assert.Multiple(
            () =>
            {
                Assert.That(began, Is.True);
                Assert.That(hasBandage, Is.True);
                Assert.That(isBandaging, Is.True);
                Assert.That(bandageService.LastBeginMobileId, Is.EqualTo((Serial)0x401u));
                Assert.That(bandageService.LastHasBandageMobileId, Is.EqualTo((Serial)0x401u));
                Assert.That(bandageService.LastIsBandagingMobileId, Is.EqualTo((Serial)0x401u));
            }
        );
    }
}
