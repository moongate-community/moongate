using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Tests.Persistence.Support;

namespace Moongate.Tests.Persistence;

public sealed class PersistenceEntityRegistryTests
{
    [Test]
    public void Register_ShouldRejectDuplicateTypeIds()
    {
        var registry = new PersistenceEntityRegistry();
        registry.Register(CreateTestDescriptor(500));

        var act = () => registry.Register(
            new PersistenceEntityDescriptor<string, int, int>(
                500,
                "duplicate-type-id",
                1,
                static entity => entity.Length,
                static entity => entity.Length,
                static snapshot => snapshot.ToString()
            )
        );

        Assert.That(act, Throws.InvalidOperationException);
    }

    [Test]
    public void Register_ShouldRejectDuplicateEntityAndKeyPairs()
    {
        var registry = new PersistenceEntityRegistry();
        registry.Register(CreateTestDescriptor(500));

        var act = () => registry.Register(CreateTestDescriptor(501));

        Assert.That(act, Throws.InvalidOperationException);
    }

    [Test]
    public void Freeze_ShouldPreventFurtherRegistrations()
    {
        var registry = new PersistenceEntityRegistry();
        registry.Register(CreateTestDescriptor(500));
        registry.Freeze();

        var act = () => registry.Register(
            new PersistenceEntityDescriptor<string, int, int>(
                501,
                "frozen-registry-type",
                1,
                static entity => entity.Length,
                static entity => entity.Length,
                static snapshot => snapshot.ToString()
            )
        );

        Assert.That(act, Throws.InvalidOperationException);
    }

    [Test]
    public void GetRegisteredDescriptors_ShouldReturnDescriptorsOrderedByTypeId()
    {
        var registry = new PersistenceEntityRegistry();
        registry.Register(
            new PersistenceEntityDescriptor<string, int, int>(
                600,
                "late",
                1,
                static entity => entity.Length,
                static entity => entity.Length,
                static snapshot => snapshot.ToString()
            )
        );
        registry.Register(CreateTestDescriptor(500));

        var descriptors = registry.GetRegisteredDescriptors().ToArray();

        Assert.That(descriptors.Select(static descriptor => descriptor.TypeId), Is.EqualTo(new ushort[] { 500, 600 }));
    }

    private static PersistenceEntityDescriptor<TestRegisteredEntity, int, (int Id, string Name)> CreateTestDescriptor(
        ushort typeId
    )
        => new(
            typeId,
            "test-registered-entity",
            1,
            static entity => entity.Id,
            static entity => (entity.Id, entity.Name),
            static snapshot => new TestRegisteredEntity
            {
                Id = snapshot.Id,
                Name = snapshot.Name
            }
        );
}
