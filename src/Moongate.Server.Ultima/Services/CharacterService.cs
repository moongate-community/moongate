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
///     Creates player characters: refuses a full account or a concurrent slot collision, picks a free slot, sanitizes other choices with
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
    private readonly ICharacterPresence _presence;

    public CharacterService(
        IDataLoaderService data,
        IStartingItemsService startingItems,
        MoongatePersistenceService persistence,
        IDataAccess<MobileEntity> mobiles,
        IMoongateEventBus events,
        CharactersConfig config,
        ICharacterPresence presence
    )
    {
        _data = data;
        _startingItems = startingItems;
        _persistence = persistence;
        _mobiles = mobiles;
        _events = events;
        _config = config;
        _presence = presence;
    }

    public async Task<CharacterCreationResult> CreateAsync(
        Serial accountId,
        CharacterCreationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        // Characters pending deletion gave up their slot and no longer count toward the limit.
        var existing = await GetCharactersAsync(accountId, cancellationToken);

        if (existing.Count(character => character.DeletionRequestedAt is null) >= _config.MaxPerAccount)
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

        return BySlot(characters);
    }

    public async Task<IReadOnlyList<MobileEntity>> GetPendingDeletionsAsync(
        Serial? accountId,
        CancellationToken cancellationToken = default
    )
    {
        var pending = accountId is { } account
            ? await _mobiles.QueryAsync(
                mobile => mobile.AccountId == account && mobile.DeletionRequestedAt != null,
                cancellationToken
            )
            : await _mobiles.QueryAsync(
                mobile => mobile.AccountId != null && mobile.DeletionRequestedAt != null,
                cancellationToken
            );

        return pending.OrderBy(character => character.DeletionRequestedAt).ToList();
    }

    public async Task<CharacterDeletionResult> RequestDeletionAsync(
        Serial accountId,
        int listIndex,
        CancellationToken cancellationToken = default
    )
    {
        // The client names the character by its position in the list it was sent, so lay the list out the same way.
        var characters = await GetCharactersAsync(accountId, cancellationToken);
        var layout = CharacterListBuilder.Layout(characters, _config.MaxPerAccount);

        if (listIndex < 0 || listIndex >= layout.Length || layout[listIndex] is not { } character)
        {
            return CharacterDeletionResult.Refused(CharacterDeleteResultType.CharacterDoesNotExist);
        }

        if (_presence.IsInWorld(character.Id))
        {
            return CharacterDeletionResult.Refused(CharacterDeleteResultType.CharacterBeingPlayed);
        }

        // The character gives up its slot, so a full account can create a new one right away; restoring puts it back
        // in a free slot when there is one.
        character.DeletionRequestedAt = DateTime.UtcNow;
        character.Slot = null;
        await _mobiles.UpsertAsync(character, cancellationToken);

        // After the save the request stands whatever happens: the event is published without cancellation.
        await _events.PublishAsync(new CharacterDeletionRequestedEvent(character), CancellationToken.None);

        return CharacterDeletionResult.Deleted(character, CharacterListBuilder.Names(characters, _config.MaxPerAccount));
    }

    public async Task<MobileEntity?> RestoreAsync(Serial characterId, CancellationToken cancellationToken = default)
    {
        var character = await _mobiles.GetByIdAsync(characterId, cancellationToken);

        if (character is not { AccountId: not null, DeletionRequestedAt: not null })
        {
            return null;
        }

        var used = (await GetCharactersAsync(character.AccountId.Value, cancellationToken))
                   .Where(other => other.DeletionRequestedAt is null && other.Slot is not null)
                   .Select(other => (int)other.Slot!.Value)
                   .ToHashSet();
        var free = Enumerable.Range(0, _config.MaxPerAccount).Where(slot => !used.Contains(slot)).ToList();

        // An account that filled up meanwhile leaves the character without a slot: the list shows it once there is room.
        character.DeletionRequestedAt = null;
        character.Slot = free.Count > 0 ? (byte)free[0] : null;
        await _mobiles.UpsertAsync(character, cancellationToken);

        return character;
    }

    private static List<MobileEntity> BySlot(IEnumerable<MobileEntity> characters)
    {
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
