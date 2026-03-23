using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Items;
using Serilog;

namespace Moongate.Server.FileLoaders;

[RegisterFileLoader(12)]
public sealed class ItemTemplateLoader : IFileLoader
{
    private enum ResolveState
    {
        Unvisited = 0,
        Visiting = 1,
        Done = 2
    }

    private readonly ILogger _logger = Log.ForContext<ItemTemplateLoader>();
    private static readonly ItemTemplateDefinition Defaults = new();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IItemTemplateService _itemTemplateService;

    public ItemTemplateLoader(DirectoriesConfig directoriesConfig, IItemTemplateService itemTemplateService)
    {
        _directoriesConfig = directoriesConfig;
        _itemTemplateService = itemTemplateService;
    }

    public Task LoadAsync()
    {
        var templatesRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Templates], "items");

        if (!Directory.Exists(templatesRootDirectory))
        {
            _logger.Warning("Item templates directory not found: {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        var templateFiles = Directory.GetFiles(templatesRootDirectory, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.Warning("No item template files found in {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        _itemTemplateService.Clear();
        var allItemTemplates = new List<ItemTemplateDefinition>();

        foreach (var templateFile in templateFiles)
        {
            ItemTemplateDefinitionBase[] templates;

            try
            {
                templates = JsonUtils.DeserializeFromFile<ItemTemplateDefinitionBase[]>(
                    templateFile,
                    MoongateUOTemplateJsonContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load item template file {TemplateFile}", templateFile);

                throw;
            }

            var itemTemplates = templates.OfType<ItemTemplateDefinition>().ToList();
            allItemTemplates.AddRange(itemTemplates);
        }

        ResolveBaseItems(allItemTemplates);
        _itemTemplateService.UpsertRange(allItemTemplates);

        _logger.Information(
            "Loaded {TemplateCount} item templates from {FileCount} files",
            allItemTemplates.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }

    private static void ApplyInheritance(ItemTemplateDefinition parent, ItemTemplateDefinition child)
    {
        if (string.IsNullOrWhiteSpace(child.Name))
        {
            child.Name = parent.Name;
        }

        if (string.IsNullOrWhiteSpace(child.Category))
        {
            child.Category = parent.Category;
        }

        if (string.IsNullOrWhiteSpace(child.Description))
        {
            child.Description = parent.Description;
        }

        if (string.IsNullOrWhiteSpace(child.ItemId))
        {
            child.ItemId = parent.ItemId;
        }

        if (string.IsNullOrWhiteSpace(child.ScriptId))
        {
            child.ScriptId = parent.ScriptId;
        }

        child.WeaponSkill ??= parent.WeaponSkill;

        if (string.IsNullOrWhiteSpace(child.GumpId))
        {
            child.GumpId = parent.GumpId;
        }

        if (string.IsNullOrWhiteSpace(child.BookId))
        {
            child.BookId = parent.BookId;
        }

        if (string.IsNullOrWhiteSpace(child.ContainerLayoutId))
        {
            child.ContainerLayoutId = parent.ContainerLayoutId;
        }

        if (child.Tags.Count == 0 && parent.Tags.Count > 0)
        {
            child.Tags = [..parent.Tags];
        }

        if (child.Container.Count == 0 && parent.Container.Count > 0)
        {
            child.Container = [..parent.Container];
        }

        if (child.LootTables.Count == 0 && parent.LootTables.Count > 0)
        {
            child.LootTables = [..parent.LootTables];
        }

        if (child.FlippableItemIds.Count == 0 && parent.FlippableItemIds.Count > 0)
        {
            child.FlippableItemIds = [..parent.FlippableItemIds];
        }

        child.Params = MergeParams(parent.Params, child.Params);

        if (child.Hue.Equals(default))
        {
            child.Hue = parent.Hue;
        }

        if (child.GoldValue.Equals(default))
        {
            child.GoldValue = parent.GoldValue;
        }

        child.Weight = InheritDecimal(child.Weight, parent.Weight, Defaults.Weight);
        child.WeightMax = InheritInt(child.WeightMax, parent.WeightMax, Defaults.WeightMax);
        child.MaxItems = InheritInt(child.MaxItems, parent.MaxItems, Defaults.MaxItems);
        child.LowDamage = InheritInt(child.LowDamage, parent.LowDamage, Defaults.LowDamage);
        child.HighDamage = InheritInt(child.HighDamage, parent.HighDamage, Defaults.HighDamage);
        child.Defense = InheritInt(child.Defense, parent.Defense, Defaults.Defense);
        child.HitPoints = InheritInt(child.HitPoints, parent.HitPoints, Defaults.HitPoints);
        child.Speed = InheritInt(child.Speed, parent.Speed, Defaults.Speed);
        child.Strength = InheritInt(child.Strength, parent.Strength, Defaults.Strength);
        child.StrengthAdd = InheritInt(child.StrengthAdd, parent.StrengthAdd, Defaults.StrengthAdd);
        child.Dexterity = InheritInt(child.Dexterity, parent.Dexterity, Defaults.Dexterity);
        child.DexterityAdd = InheritInt(child.DexterityAdd, parent.DexterityAdd, Defaults.DexterityAdd);
        child.Intelligence = InheritInt(child.Intelligence, parent.Intelligence, Defaults.Intelligence);
        child.IntelligenceAdd = InheritInt(child.IntelligenceAdd, parent.IntelligenceAdd, Defaults.IntelligenceAdd);
        child.PhysicalResist = InheritInt(child.PhysicalResist, parent.PhysicalResist, Defaults.PhysicalResist);
        child.FireResist = InheritInt(child.FireResist, parent.FireResist, Defaults.FireResist);
        child.ColdResist = InheritInt(child.ColdResist, parent.ColdResist, Defaults.ColdResist);
        child.PoisonResist = InheritInt(child.PoisonResist, parent.PoisonResist, Defaults.PoisonResist);
        child.EnergyResist = InheritInt(child.EnergyResist, parent.EnergyResist, Defaults.EnergyResist);
        child.HitChanceIncrease = InheritInt(
            child.HitChanceIncrease,
            parent.HitChanceIncrease,
            Defaults.HitChanceIncrease
        );
        child.DefenseChanceIncrease = InheritInt(
            child.DefenseChanceIncrease,
            parent.DefenseChanceIncrease,
            Defaults.DefenseChanceIncrease
        );
        child.DamageIncrease = InheritInt(child.DamageIncrease, parent.DamageIncrease, Defaults.DamageIncrease);
        child.SwingSpeedIncrease = InheritInt(
            child.SwingSpeedIncrease,
            parent.SwingSpeedIncrease,
            Defaults.SwingSpeedIncrease
        );
        child.SpellDamageIncrease = InheritInt(
            child.SpellDamageIncrease,
            parent.SpellDamageIncrease,
            Defaults.SpellDamageIncrease
        );
        child.FasterCasting = InheritInt(child.FasterCasting, parent.FasterCasting, Defaults.FasterCasting);
        child.FasterCastRecovery = InheritInt(
            child.FasterCastRecovery,
            parent.FasterCastRecovery,
            Defaults.FasterCastRecovery
        );
        child.LowerManaCost = InheritInt(child.LowerManaCost, parent.LowerManaCost, Defaults.LowerManaCost);
        child.LowerReagentCost = InheritInt(
            child.LowerReagentCost,
            parent.LowerReagentCost,
            Defaults.LowerReagentCost
        );
        child.Luck = InheritInt(child.Luck, parent.Luck, Defaults.Luck);
        child.UsesRemaining = InheritInt(child.UsesRemaining, parent.UsesRemaining, Defaults.UsesRemaining);
        child.LowerAmmoCost = InheritInt(child.LowerAmmoCost, parent.LowerAmmoCost, Defaults.LowerAmmoCost);
        child.QuiverDamageIncrease = InheritInt(
            child.QuiverDamageIncrease,
            parent.QuiverDamageIncrease,
            Defaults.QuiverDamageIncrease
        );
        child.WeightReduction = InheritInt(child.WeightReduction, parent.WeightReduction, Defaults.WeightReduction);
        child.Ammo = InheritInt(child.Ammo, parent.Ammo, Defaults.Ammo);
        child.AmmoFx = InheritInt(child.AmmoFx, parent.AmmoFx, Defaults.AmmoFx);
        child.MaxRange = InheritInt(child.MaxRange, parent.MaxRange, Defaults.MaxRange);
        child.BaseRange = InheritInt(child.BaseRange, parent.BaseRange, Defaults.BaseRange);
        child.HitSound ??= parent.HitSound;
        child.MissSound ??= parent.MissSound;

        child.IsQuiver = InheritBool(child.IsQuiver, parent.IsQuiver, Defaults.IsQuiver);
        child.Dyeable = InheritBool(child.Dyeable, parent.Dyeable, Defaults.Dyeable);
        child.IsMovable = InheritBool(child.IsMovable, parent.IsMovable, Defaults.IsMovable);
        child.SpellChanneling = InheritBool(
            child.SpellChanneling,
            parent.SpellChanneling,
            Defaults.SpellChanneling
        );

        if (child.LootType == Defaults.LootType)
        {
            child.LootType = parent.LootType;
        }

        if (child.Visibility == Defaults.Visibility)
        {
            child.Visibility = parent.Visibility;
        }
    }

    private static ItemTemplateParamDefinition CloneParam(ItemTemplateParamDefinition param)
        => new()
        {
            Type = param.Type,
            Value = param.Value
        };

    private static bool InheritBool(bool childValue, bool parentValue, bool defaultValue)
        => childValue == defaultValue ? parentValue : childValue;

    private static decimal InheritDecimal(decimal childValue, decimal parentValue, decimal defaultValue)
        => childValue == defaultValue ? parentValue : childValue;

    private static int InheritInt(int childValue, int parentValue, int defaultValue)
        => childValue == defaultValue ? parentValue : childValue;

    private static Dictionary<string, ItemTemplateParamDefinition> MergeParams(
        Dictionary<string, ItemTemplateParamDefinition> parentParams,
        Dictionary<string, ItemTemplateParamDefinition> childParams
    )
    {
        var merged = new Dictionary<string, ItemTemplateParamDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, param) in parentParams)
        {
            merged[key] = CloneParam(param);
        }

        foreach (var (key, param) in childParams)
        {
            merged[key] = CloneParam(param);
        }

        return merged;
    }

    private static void ResolveBaseItems(List<ItemTemplateDefinition> templates)
    {
        var byId = templates.ToDictionary(
            static template => template.Id,
            static template => template,
            StringComparer.OrdinalIgnoreCase
        );

        var states = new Dictionary<string, ResolveState>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            ResolveTemplate(template, byId, states);
        }
    }

    private static void ResolveTemplate(
        ItemTemplateDefinition template,
        Dictionary<string, ItemTemplateDefinition> byId,
        Dictionary<string, ResolveState> states
    )
    {
        if (states.TryGetValue(template.Id, out var state))
        {
            if (state == ResolveState.Done)
            {
                return;
            }

            if (state == ResolveState.Visiting)
            {
                throw new InvalidOperationException($"Circular base_item reference detected at '{template.Id}'.");
            }
        }

        states[template.Id] = ResolveState.Visiting;

        if (!string.IsNullOrWhiteSpace(template.BaseItem))
        {
            if (!byId.TryGetValue(template.BaseItem, out var parent))
            {
                throw new InvalidOperationException(
                    $"Template '{template.Id}' references unknown base_item '{template.BaseItem}'."
                );
            }

            ResolveTemplate(parent, byId, states);
            ApplyInheritance(parent, template);
        }

        states[template.Id] = ResolveState.Done;
    }
}
