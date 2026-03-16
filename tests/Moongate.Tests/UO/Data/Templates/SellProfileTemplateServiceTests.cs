using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.SellProfiles;

namespace Moongate.Tests.UO.Data.Templates;

public class SellProfileTemplateServiceTests
{
    private SellProfileTemplateService _service = null!;

    [Test]
    public void Clear_ShouldRemoveProfiles()
    {
        _service.Upsert(CreateDefinition("vendor.blacksmith", "Blacksmith Vendor"));

        _service.Clear();

        Assert.That(_service.Count, Is.Zero);
    }

    [SetUp]
    public void SetUp()
        => _service = new();

    [Test]
    public void Upsert_ShouldRegisterProfile()
    {
        var definition = CreateDefinition("vendor.blacksmith", "Blacksmith Vendor");

        _service.Upsert(definition);

        Assert.Multiple(
            () =>
            {
                Assert.That(_service.Count, Is.EqualTo(1));
                Assert.That(_service.TryGet("vendor.blacksmith", out var resolved), Is.True);
                Assert.That(resolved?.Name, Is.EqualTo("Blacksmith Vendor"));
            }
        );
    }

    [Test]
    public void UpsertRange_ShouldRegisterProfiles()
    {
        _service.UpsertRange(
            [
                CreateDefinition("vendor.blacksmith", "Blacksmith Vendor"),
                CreateDefinition("vendor.barkeep", "Barkeep Vendor")
            ]
        );

        Assert.That(_service.Count, Is.EqualTo(2));
    }

    private static SellProfileTemplateDefinition CreateDefinition(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Category = "vendors",
            Description = name
        };
}
