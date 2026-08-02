using Moongate.Http.Plugin.Services.Characters;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Http.Characters;

/// <summary>
/// The mobile stores skills in tenths under a bare id. What this reader owns is turning that into
/// something a reader can use: a name, and a value in points.
/// </summary>
public class CharacterSkillReaderTests
{
    [Fact]
    public void Read_NamesEachSkillFromTheRegistry()
    {
        var mobile = With((1, 500, 1000, SkillLockType.Up));

        var skills = new CharacterSkillReader(Registry((1, "Swordsmanship"))).Read(mobile);

        Assert.Equal("Swordsmanship", Assert.Single(skills).Name);
    }

    // 500 tenths is 50.0 points. Reporting the tenths would leave every consumer to remember the
    // division, and the first one to forget publishes a character with 500.0 swordsmanship.
    [Fact]
    public void Read_ReportsPointsRatherThanTheStoredTenths()
    {
        var mobile = With((1, 500, 1000, SkillLockType.Up));

        var skill = Assert.Single(new CharacterSkillReader(Registry((1, "Swordsmanship"))).Read(mobile));

        Assert.Equal(50.0, skill.Value);
        Assert.Equal(100.0, skill.Cap);
    }

    [Fact]
    public void Read_ReportsTheLock()
    {
        var mobile = With((1, 500, 1000, SkillLockType.Locked));

        Assert.Equal("Locked", Assert.Single(new CharacterSkillReader(Registry((1, "Swordsmanship"))).Read(mobile)).Lock);
    }

    // The skill catalogue is data, so it can lag the world: a character can hold a skill nothing
    // defines. Its id is worse than a name and far better than a blank row.
    [Fact]
    public void Read_FallsBackToTheIdWhenNoDefinitionIsRegistered()
    {
        var mobile = With((42, 300, 1000, SkillLockType.Up));

        Assert.Equal("42", Assert.Single(new CharacterSkillReader(Registry()).Read(mobile)).Name);
    }

    // The dictionary is keyed by id and iterates in insertion order, which is whatever order the
    // creation packet happened to use. A list a human reads should be alphabetical.
    [Fact]
    public void Read_OrdersByName()
    {
        var mobile = With((1, 500, 1000, SkillLockType.Up), (2, 400, 1000, SkillLockType.Up));

        var skills = new CharacterSkillReader(Registry((1, "Swordsmanship"), (2, "Alchemy"))).Read(mobile);

        Assert.Equal(["Alchemy", "Swordsmanship"], skills.Select(skill => skill.Name));
    }

    [Fact]
    public void Read_IsEmptyForACharacterWithNoSkills()
        => Assert.Empty(new CharacterSkillReader(Registry()).Read(new MobileEntity { Id = new(1) }));

    private static MobileEntity With(params (int Id, int Value, int Cap, SkillLockType Lock)[] skills)
    {
        var mobile = new MobileEntity { Id = new(1) };

        foreach (var (id, value, cap, locked) in skills)
        {
            mobile.Skills[id] = new() { Value = value, Cap = cap, Lock = locked };
        }

        return mobile;
    }

    private static ISkillService Registry(params (int Id, string Name)[] definitions)
        => new SkillRegistryStub(definitions);

    private sealed class SkillRegistryStub : ISkillService
    {
        private readonly Dictionary<int, SkillDefinition> _definitions;

        public SkillRegistryStub(IEnumerable<(int Id, string Name)> definitions)
        {
            _definitions = definitions.ToDictionary(
                definition => definition.Id,
                definition => new SkillDefinition { Id = definition.Id, Name = definition.Name }
            );
        }

        public IReadOnlyList<SkillDefinition> All
            => [.. _definitions.Values];

        public int Count
            => _definitions.Count;

        public SkillDefinition? GetById(int id)
            => _definitions.GetValueOrDefault(id);

        public SkillDefinition? GetByName(string name)
            => throw new NotSupportedException();

        public void Register(SkillDefinition definition)
            => throw new NotSupportedException();
    }
}
