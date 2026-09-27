using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Names;
using Moongate.Server.Ultima.Data.Professions;
using Moongate.Server.Ultima.Data.Races;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Characters;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;
using Npgsql;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Creates player characters: refuses only a full account, picks a free slot, sanitizes every other choice with
///     <see cref="CharacterCreationRules" />, and saves the character with its starting items in one transaction.
/// </summary>
public sealed class CharacterService : ICharacterService
{
    private const string UniqueSlotIndex = "ux_mobiles_account_slot";
    private const string UniqueViolation = "23505";

    private readonly IDataLoaderService _data;
    private readonly IStartingItemsService _startingItems;
    private readonly MoongatePersistenceService _persistence;
    private readonly IDataAccess<MobileEntity> _mobiles;
    private readonly IMoongateEventBus _events;
    private readonly CharactersConfig _config;

    public CharacterService(
        IDataLoaderService data,
        IStartingItemsService startingItems,
        MoongatePersistenceService persistence,
        IDataAccess<MobileEntity> mobiles,
        IMoongateEventBus events,
        CharactersConfig config
    )
    {
        _data = data;
        _startingItems = startingItems;
        _persistence = persistence;
        _mobiles = mobiles;
        _events = events;
        _config = config;
    }

    public async Task<CharacterCreationResult> CreateAsync(
        Serial accountId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await GetCharactersAsync(accountId, cancellationToken);

        if (existing.Count >= _config.MaxPerAccount)
        {
            return CharacterCreationResult.Refused(CharacterCreationRefusalType.TooManyCharacters);
        }

        var character = Build(accountId, request, FreeSlot(request.Slot, existing));
        IReadOnlyList<ItemEntity> items = [];

        try
        {
            await _persistence.ExecuteInTransactionAsync(
                PersistenceDatabaseTarget.Realm,
                async transaction =>
                {
                    await transaction.GetDataAccess<MobileEntity>().UpsertAsync(character, cancellationToken);

                    if (!character.Id.IsMobile)
                    {
                        throw new InvalidOperationException(
                            $"Character '{character.Name}' was saved with {character.Id}, outside the mobile range."
                        );
                    }

                    items = await _startingItems.GiveAsync(
                        transaction,
                        StartingItemsRequestFor(character, request),
                        cancellationToken
                    );
                },
                cancellationToken
            );
        }
        catch (Exception exception) when (IsSlotCollision(exception))
        {
            // Another create for the same slot committed first; the rollback undid this one.
            character.Id = Serial.Zero;

            return CharacterCreationResult.Refused(CharacterCreationRefusalType.SlotUnavailable);
        }
        catch
        {
            // The rollback undid the rows; the in-memory character must not keep a serial that does not exist.
            character.Id = Serial.Zero;

            throw;
        }

        // After the commit the character is saved whatever happens: the event is published without cancellation.
        await _events.PublishAsync(new CharacterCreatedEvent(character, items), CancellationToken.None);

        return CharacterCreationResult.Created(character, items);
    }

    public async Task<IReadOnlyList<MobileEntity>> GetCharactersAsync(
        Serial accountId,
        CancellationToken cancellationToken = default
    )
    {
        // A plain read: a transaction would queue behind every realm write. The unique slot index, not this count,
        // is what stops two creates from sharing a slot.
        var characters = await _mobiles.QueryAsync(mobile => mobile.AccountId == accountId, cancellationToken);

        return characters.OrderBy(character => character.Slot ?? byte.MaxValue).ThenBy(character => character.Id).ToList();
    }

    /// <summary>
    ///     The requested slot when it is in range and free, otherwise the first free one. Established servers ignore the
    ///     client's slot altogether; keeping it when it fits preserves the player's choice without trusting its meaning.
    ///     An account below its limit always has a free slot.
    /// </summary>
    private byte FreeSlot(int requested, IReadOnlyList<MobileEntity> existing)
    {
        var used = existing.Where(c => c.Slot is not null).Select(c => (int)c.Slot!.Value).ToHashSet();

        if (requested >= 0 && requested < _config.MaxPerAccount && !used.Contains(requested))
        {
            return (byte)requested;
        }

        return (byte)Enumerable.Range(0, _config.MaxPerAccount).First(slot => !used.Contains(slot));
    }

    private MobileEntity Build(Serial accountId, CharacterCreationRequest request, byte slot)
    {
        var race = _data.GetEntities<RaceContent>().FirstOrDefault(content => content.Race == request.Race) ??
                   _data.GetEntities<RaceContent>().FirstOrDefault(content => content.Race == RaceType.Human) ??
                   throw new InvalidDataException("No human race is loaded from races.toml.");
        var gender = request.Gender == GenderType.Female ? GenderType.Female : GenderType.Male;
        var city = StartingCity(request.StartingCity);
        var (strength, dexterity, intelligence, skills) = StatsAndSkills(request, race.Race);
        var (hairStyle, hairHue) = CharacterCreationRules.ValidateHair(race, gender, request.HairStyle, request.HairHue);
        var (beardStyle, beardHue) = CharacterCreationRules.ValidateBeard(race, gender, request.BeardStyle, request.BeardHue);
        var bannedNames = _data.GetEntities<BannedNamesContent>().FirstOrDefault() ?? new BannedNamesContent();

        return new()
        {
            AccountId = accountId,
            Slot = slot,
            Name = CharacterCreationRules.ValidateName(request.Name, bannedNames),
            Gender = gender,
            Race = race.Race,
            Body = race.For(gender).Body,
            SkinHue = CharacterCreationRules.ValidateSkinHue(race, request.SkinHue),
            HairStyle = hairStyle,
            HairHue = hairHue,
            BeardStyle = beardStyle,
            BeardHue = beardHue,
            Strength = strength,
            Dexterity = dexterity,
            Intelligence = intelligence,
            Hits = strength,
            HitsMax = strength,
            Stamina = dexterity,
            StaminaMax = dexterity,
            Mana = intelligence,
            ManaMax = intelligence,
            Skills = skills.Select(skill => new MobileSkill { Skill = skill.Skill, Base = skill.Value * 10 }).ToList(),
            Notoriety = NotorietyType.Innocent,
            Location = city.Location,
            Map = city.Map,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     A known profession other than 0 ("Advanced") gives its own stats and skills; otherwise the player's choices
    ///     are validated.
    /// </summary>
    private (int Strength, int Dexterity, int Intelligence, IReadOnlyList<CharacterSkillChoice> Skills) StatsAndSkills(
        CharacterCreationRequest request,
        RaceType race
    )
    {
        var profession = request.Profession == 0
            ? null
            : _data.GetEntities<ProfessionContent>().FirstOrDefault(content => content.Id == request.Profession);

        if (profession is not null)
        {
            return (profession.Str, profession.Dex, profession.Int,
                    profession.Skills.Select(skill => new CharacterSkillChoice { Skill = skill.Skill, Value = (byte)skill.Value })
                              .ToList());
        }

        var (strength, dexterity, intelligence) =
            CharacterCreationRules.ValidateStats(request.Strength, request.Dexterity, request.Intelligence);

        return (strength, dexterity, intelligence, CharacterCreationRules.ValidateSkills(request.Skills, race));
    }

    private StartingCityContent StartingCity(int index)
    {
        var cities = _data.GetEntities<StartingCityContent>();

        if (cities.Count == 0)
        {
            throw new InvalidDataException("No starting city is loaded from starting_cities.toml.");
        }

        return index >= 0 && index < cities.Count ? cities[index] : cities[0];
    }

    private static StartingItemsRequest StartingItemsRequestFor(MobileEntity character, CharacterCreationRequest request)
    {
        return new(
            character.Id,
            character.Race,
            character.Gender,
            character.Skills.ToDictionary(skill => skill.Skill, skill => skill.Base / 10),
            ClothingHue(request.ShirtHue),
            ClothingHue(request.PantsHue)
        );
    }

    /// <summary>
    ///     0 keeps the template's hue (the Enhanced Client has no pants choice); any other hue is clipped to the dyeable
    ///     range.
    /// </summary>
    private static Hue ClothingHue(Hue hue)
    {
        return hue.Value == 0 ? hue : CharacterCreationRules.ValidateClothingHue(hue);
    }

    private static bool IsSlotCollision(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: UniqueViolation, ConstraintName: UniqueSlotIndex })
            {
                return true;
            }
        }

        return false;
    }
}
