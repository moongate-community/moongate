using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Magic;
using Moongate.Server.Types.Commands;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "give_magery_test_kit",
    "Prepare the caller for magery testing. Usage: .give_magery_test_kit",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class GiveMageryTestKitCommand : ICommandExecutor
{
    private const string SpellbookTemplateId = "spellbook";
    private const int TargetMageryValue = 1000;
    private const int MinimumIntelligence = 100;
    private const int ReagentStackAmount = 100;

    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ICharacterService _characterService;
    private readonly ISpellbookService _spellbookService;
    private readonly SpellRegistry _spellRegistry;

    public GiveMageryTestKitCommand(
        IItemService itemService,
        IGameNetworkSessionService gameNetworkSessionService,
        ICharacterService characterService,
        ISpellbookService spellbookService,
        SpellRegistry spellRegistry
    )
    {
        ArgumentNullException.ThrowIfNull(itemService);
        ArgumentNullException.ThrowIfNull(gameNetworkSessionService);
        ArgumentNullException.ThrowIfNull(characterService);
        ArgumentNullException.ThrowIfNull(spellbookService);
        ArgumentNullException.ThrowIfNull(spellRegistry);

        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _characterService = characterService;
        _spellbookService = spellbookService;
        _spellRegistry = spellRegistry;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .give_magery_test_kit");

            return;
        }

        if (!_gameNetworkSessionService.TryGet(context.SessionId, out var session) || session.Character is null)
        {
            context.PrintError("Failed to prepare magery kit: no active player found.");

            return;
        }

        var player = session.Character;
        var backpack = await _characterService.GetBackpackWithItemsAsync(player);

        if (backpack is null || backpack.Id == 0)
        {
            context.PrintError("Failed to prepare magery kit: backpack not found.");

            return;
        }

        try
        {
            PrepareStats(player);

            var spellbook = await EnsureSpellbookAsync(player, backpack, context.SessionId);
            var spellbookData = BuildRegularSpellbookData();
            _spellbookService.SetData(spellbook, spellbookData);
            await _itemService.UpsertItemAsync(spellbook);

            var reagentTemplateIds = GetRequiredReagentTemplateIds();
            await AddReagentsAsync(backpack.Id, reagentTemplateIds, context.SessionId);

            context.Print(
                "Prepared magery test kit: Magery={0}, spells={1}, reagents={2}.",
                player.Skills[UOSkillName.Magery].Value,
                CountSpells(spellbookData),
                reagentTemplateIds.Count
            );
        }
        catch (Exception exception)
        {
            context.PrintError("Failed to prepare magery kit: {0}", exception.Message);
        }
    }

    private void PrepareStats(UOMobileEntity player)
    {
        player.SetSkill(UOSkillName.Magery, TargetMageryValue, cap: TargetMageryValue, lockState: UOSkillLock.Up);

        if (player.Intelligence < MinimumIntelligence)
        {
            player.Intelligence = MinimumIntelligence;
        }

        player.RecalculateMaxStats();
        player.Mana = player.MaxMana;
    }

    private async Task<UOItemEntity> EnsureSpellbookAsync(UOMobileEntity player, UOItemEntity backpack, long sessionId)
    {
        var existingSpellbook = await _spellbookService.FindSpellbookAsync(player, SpellbookType.Regular);

        if (existingSpellbook is not null)
        {
            return existingSpellbook;
        }

        var spawnedSpellbook = await _itemService.SpawnFromTemplateAsync(SpellbookTemplateId);
        await _itemService.UpsertItemAsync(spawnedSpellbook);

        var moved = await _itemService.MoveItemToContainerAsync(spawnedSpellbook.Id, backpack.Id, new(1, 1), sessionId);

        if (!moved)
        {
            throw new InvalidOperationException("could not move spellbook into backpack");
        }

        return spawnedSpellbook;
    }

    private SpellbookData BuildRegularSpellbookData()
    {
        var data = new SpellbookData(0UL);

        foreach (var spell in _spellRegistry.All.Values
                     .Where(static spell => spell.SpellbookType == SpellbookType.Regular && spell.SpellId is >= 1 and <= 64)
                     .OrderBy(static spell => spell.SpellId))
        {
            data = data.WithSpell(spell.SpellId);
        }

        return data;
    }

    private List<string> GetRequiredReagentTemplateIds()
    {
        var templateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spell in _spellRegistry.All.Values.Where(static spell => spell.SpellbookType == SpellbookType.Regular))
        {
            foreach (var reagent in spell.Info.Reagents)
            {
                var reagentTemplateId = ReagentCatalog.GetTemplateId(reagent);

                if (!string.IsNullOrWhiteSpace(reagentTemplateId))
                {
                    templateIds.Add(reagentTemplateId);
                }
            }
        }

        return templateIds.OrderBy(static templateId => templateId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task AddReagentsAsync(Serial backpackId, IReadOnlyList<string> reagentTemplateIds, long sessionId)
    {
        foreach (var reagentTemplateId in reagentTemplateIds)
        {
            var item = await _itemService.SpawnFromTemplateAsync(reagentTemplateId);

            if (item.IsStackable)
            {
                item.Amount = ReagentStackAmount;
            }

            await _itemService.UpsertItemAsync(item);

            var moved = await _itemService.MoveItemToContainerAsync(item.Id, backpackId, new(1, 1), sessionId);

            if (!moved)
            {
                throw new InvalidOperationException($"could not move reagent '{reagentTemplateId}' into backpack");
            }
        }
    }

    private static int CountSpells(SpellbookData data)
    {
        var count = 0;

        for (var spellId = 1; spellId <= 64; spellId++)
        {
            if (data.HasSpell(spellId))
            {
                count++;
            }
        }

        return count;
    }
}
