using Moongate.Persistence.Snapshots;

namespace Moongate.Persistence.Tests.Snapshots;

public sealed class SnapshotFingerprintTests
{
    [Fact]
    public void APropertyWithAPrivateSetter_ChangesTheFingerprint()
    {
        var before = new Durable();
        var after = new Durable();
        after.Damage();

        Assert.NotEqual(SnapshotFingerprint.Of(before), SnapshotFingerprint.Of(after));
    }

    [Fact]
    public void AComputedPropertyThatThrows_IsLeftOut()
    {
        var fingerprint = SnapshotFingerprint.Of(new Durable());

        Assert.Equal(fingerprint, SnapshotFingerprint.Of(new Durable()));
    }

    [Fact]
    public void ANotANumberValue_HasAFingerprint()
    {
        var odd = new Dictionary<string, object?> { ["ratio"] = double.NaN, ["far"] = double.PositiveInfinity };

        Assert.NotEqual(SnapshotFingerprint.Of(odd), SnapshotFingerprint.Of(new Dictionary<string, object?>()));
    }

    private sealed class Durable
    {
        public int Durability { get; private set; } = 10;

        public int Computed => throw new InvalidOperationException("computed on a partial state");

        public void Damage()
        {
            Durability--;
        }
    }
}
