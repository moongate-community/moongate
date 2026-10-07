using System.Reflection;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Snapshots;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Entities.World;

/// <summary>
///     The world save writes an entity only when its snapshot fingerprint changes: every saved property must change it,
///     or a change to that property alone would never reach the database.
/// </summary>
public sealed class EntityFingerprintCoverageTests
{
    public static TheoryData<string> ItemProperties => Names<ItemEntity>();

    public static TheoryData<string> MobileProperties => Names<MobileEntity>();

    [Theory, MemberData(nameof(ItemProperties))]
    public void EveryItemProperty_ChangesTheFingerprint(string property)
    {
        var item = new ItemEntity
        {
            Id = new Serial(0x40000001), TemplateId = "dagger", ItemId = 0x0F52, Amount = 1,
            Props = new() { ["key.value"] = 1L }
        };
        item.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));

        AssertChanges(item.Snapshot(), item.Snapshot(), property);
    }

    [Theory, MemberData(nameof(MobileProperties))]
    public void EveryMobileProperty_ChangesTheFingerprint(string property)
    {
        var mobile = new MobileEntity
        {
            Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Trammel,
            Location = new Point3D(1600, 1600, 0), Props = new() { ["vega.greeted"] = 1L },
            Skills = [new MobileSkill { Skill = SkillType.Alchemy, Base = 100 }]
        };

        AssertChanges(mobile.Snapshot(), mobile.Snapshot(), property);
    }

    private static void AssertChanges<T>(T baseline, T changed, string name)
    {
        var property = typeof(T).GetProperty(name)!;
        property.SetValue(changed, Different(property.PropertyType, property.GetValue(changed)));

        Assert.NotEqual(SnapshotFingerprint.Of(baseline!), SnapshotFingerprint.Of(changed!));
    }

    private static TheoryData<string> Names<T>()
    {
        var data = new TheoryData<string>();

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.SetMethod is not null))
        {
            data.Add(property.Name);
        }

        return data;
    }

    // A value of the type that differs from the current one; a type it does not know fails, so a new kind of
    // property gets a case here.
    private static object Different(Type type, object? current)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying);

            foreach (var value in values)
            {
                if (!value.Equals(current))
                {
                    return value;
                }
            }
        }

        return underlying switch
        {
            _ when underlying == typeof(int) => (current is int number ? number : 0) + 7,
            _ when underlying == typeof(short) => (short)((current is short number ? number : 0) + 7),
            _ when underlying == typeof(long) => (current is long number ? number : 0) + 7,
            _ when underlying == typeof(bool) => !(current is true),
            _ when underlying == typeof(string) => (current as string ?? "") + "x",
            _ when underlying == typeof(Serial) => new Serial((current is Serial serial ? serial.Value : 0) + 7),
            _ when underlying == typeof(Hue) => new Hue((ushort)((current is Hue hue ? hue.Value : 0) + 7)),
            _ when underlying == typeof(Point3D) => new Point3D(1, 2, 3),
            _ when underlying == typeof(DateTime) => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ when underlying == typeof(DateTimeOffset) => new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero),
            _ when underlying == typeof(Dictionary<string, object?>) => new Dictionary<string, object?> { ["other"] = 2L },
            _ when underlying == typeof(List<MobileSkill>) => new List<MobileSkill>
                { new() { Skill = SkillType.Archery, Base = 5 } },
            _ => throw new InvalidOperationException($"No different value known for {type}: add one.")
        };
    }
}
