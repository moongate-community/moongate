using Moongate.Generators.Annotations.Persistence;

namespace Moongate.Tests.Generators.Persistence;

public sealed class PersistenceAnnotationsAvailabilityTests
{
    [Test]
    public void MoongatePersistedEntityAttribute_ShouldBeDefined()
    {
        Assert.Multiple(
            () =>
            {
                Assert.That(typeof(MoongatePersistedEntityAttribute).IsSealed, Is.True);
                Assert.That(typeof(MoongatePersistedMemberAttribute).IsSealed, Is.True);
                Assert.That(typeof(MoongatePersistedIgnoreAttribute).IsSealed, Is.True);
                Assert.That(typeof(MoongatePersistedConverterAttribute).IsSealed, Is.True);
            }
        );
    }
}
