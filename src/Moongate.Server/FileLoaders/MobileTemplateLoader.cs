using Moongate.Core.Data.Directories;
using Moongate.Core.Json;
using Moongate.Core.Types;
using Moongate.Server.Attributes;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Json.Context;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Serilog;

namespace Moongate.Server.FileLoaders;

/// <summary>
/// Loads mobile templates from <c>templates/mobiles</c> into <see cref="IMobileTemplateService" />.
/// </summary>
[RegisterFileLoader(13)]
public sealed class MobileTemplateLoader : IFileLoader
{
    private enum ResolveState
    {
        Unvisited = 0,
        Visiting = 1,
        Done = 2
    }

    private readonly ILogger _logger = Log.ForContext<MobileTemplateLoader>();
    private static readonly MobileTemplateDefinition Defaults = new();
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IMobileTemplateService _mobileTemplateService;

    public MobileTemplateLoader(DirectoriesConfig directoriesConfig, IMobileTemplateService mobileTemplateService)
    {
        _directoriesConfig = directoriesConfig;
        _mobileTemplateService = mobileTemplateService;
    }

    public Task LoadAsync()
    {
        var templatesRootDirectory = Path.Combine(_directoriesConfig[DirectoryType.Templates], "mobiles");

        if (!Directory.Exists(templatesRootDirectory))
        {
            _logger.Warning("Mobile templates directory not found: {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        var templateFiles = Directory.GetFiles(templatesRootDirectory, "*.json", SearchOption.AllDirectories);

        if (templateFiles.Length == 0)
        {
            _logger.Warning("No mobile template files found in {Directory}", templatesRootDirectory);

            return Task.CompletedTask;
        }

        _mobileTemplateService.Clear();
        var allMobileTemplates = new List<MobileTemplateDefinition>();

        foreach (var templateFile in templateFiles)
        {
            MobileTemplateDefinitionBase[] templates;

            try
            {
                templates = JsonUtils.DeserializeFromFile<MobileTemplateDefinitionBase[]>(
                    templateFile,
                    MoongateUOTemplateJsonContext.Default
                );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load mobile template file {TemplateFile}", templateFile);

                throw;
            }

            var mobileTemplates = templates.OfType<MobileTemplateDefinition>().ToList();

            foreach (var mobileTemplate in mobileTemplates)
            {
                NormalizeTitleAndName(mobileTemplate);
            }
            allMobileTemplates.AddRange(mobileTemplates);
        }

        ResolveBaseMobiles(allMobileTemplates);
        _mobileTemplateService.UpsertRange(allMobileTemplates);

        _logger.Information(
            "Loaded {TemplateCount} mobile templates from {FileCount} files",
            allMobileTemplates.Count,
            templateFiles.Length
        );

        return Task.CompletedTask;
    }

    private static void ApplyInheritance(MobileTemplateDefinition parent, MobileTemplateDefinition child)
    {
        if (string.IsNullOrWhiteSpace(child.Title))
        {
            child.Title = parent.Title;
        }

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

        if (child.Tags.Count == 0 && parent.Tags.Count > 0)
        {
            child.Tags = [..parent.Tags];
        }

        if (child.Body == Defaults.Body)
        {
            child.Body = parent.Body;
        }

        if (child.SkinHue.Equals(default))
        {
            child.SkinHue = parent.SkinHue;
        }

        if (child.HairHue.Equals(default))
        {
            child.HairHue = parent.HairHue;
        }

        if (child.HairStyle == Defaults.HairStyle)
        {
            child.HairStyle = parent.HairStyle;
        }

        child.Strength = InheritInt(child.Strength, parent.Strength, Defaults.Strength);
        child.Dexterity = InheritInt(child.Dexterity, parent.Dexterity, Defaults.Dexterity);
        child.Intelligence = InheritInt(child.Intelligence, parent.Intelligence, Defaults.Intelligence);
        child.Hits = InheritInt(child.Hits, parent.Hits, Defaults.Hits);
        child.MaxHits = InheritInt(child.MaxHits, parent.MaxHits, Defaults.MaxHits);
        child.Mana = InheritInt(child.Mana, parent.Mana, Defaults.Mana);
        child.Stamina = InheritInt(child.Stamina, parent.Stamina, Defaults.Stamina);
        child.MinDamage = InheritInt(child.MinDamage, parent.MinDamage, Defaults.MinDamage);
        child.MaxDamage = InheritInt(child.MaxDamage, parent.MaxDamage, Defaults.MaxDamage);
        child.ArmorRating = InheritInt(child.ArmorRating, parent.ArmorRating, Defaults.ArmorRating);
        child.Fame = InheritInt(child.Fame, parent.Fame, Defaults.Fame);
        child.Karma = InheritInt(child.Karma, parent.Karma, Defaults.Karma);

        if (child.Notoriety == Defaults.Notoriety)
        {
            child.Notoriety = parent.Notoriety;
        }

        if (string.Equals(child.Brain, Defaults.Brain, StringComparison.OrdinalIgnoreCase))
        {
            child.Brain = parent.Brain;
        }

        if (string.IsNullOrWhiteSpace(child.SellProfileId))
        {
            child.SellProfileId = parent.SellProfileId;
        }

        if (string.IsNullOrWhiteSpace(child.DefaultFactionId))
        {
            child.DefaultFactionId = parent.DefaultFactionId;
        }

        if (child.Sounds.Count == 0 && parent.Sounds.Count > 0)
        {
            child.Sounds = new(parent.Sounds);
        }
        else if (parent.Sounds.Count > 0)
        {
            foreach (var kvp in parent.Sounds)
            {
                child.Sounds.TryAdd(kvp.Key, kvp.Value);
            }
        }

        if (child.GoldDrop.Equals(default))
        {
            child.GoldDrop = parent.GoldDrop;
        }

        if (child.LootTables.Count == 0 && parent.LootTables.Count > 0)
        {
            child.LootTables = [..parent.LootTables];
        }

        if (child.Skills.Count == 0 && parent.Skills.Count > 0)
        {
            child.Skills = new(parent.Skills, StringComparer.OrdinalIgnoreCase);
        }
        else if (parent.Skills.Count > 0)
        {
            foreach (var kvp in parent.Skills)
            {
                child.Skills.TryAdd(kvp.Key, kvp.Value);
            }
        }

        if (child.Resistances.Count == 0 && parent.Resistances.Count > 0)
        {
            child.Resistances = new(parent.Resistances, StringComparer.OrdinalIgnoreCase);
        }
        else if (parent.Resistances.Count > 0)
        {
            foreach (var kvp in parent.Resistances)
            {
                child.Resistances.TryAdd(kvp.Key, kvp.Value);
            }
        }

        if (child.DamageTypes.Count == 0 && parent.DamageTypes.Count > 0)
        {
            child.DamageTypes = new(parent.DamageTypes, StringComparer.OrdinalIgnoreCase);
        }
        else if (parent.DamageTypes.Count > 0)
        {
            foreach (var kvp in parent.DamageTypes)
            {
                child.DamageTypes.TryAdd(kvp.Key, kvp.Value);
            }
        }

        child.TamingDifficulty = InheritInt(
            child.TamingDifficulty,
            parent.TamingDifficulty,
            Defaults.TamingDifficulty
        );
        child.ProvocationDifficulty = InheritInt(
            child.ProvocationDifficulty,
            parent.ProvocationDifficulty,
            Defaults.ProvocationDifficulty
        );
        child.PacificationDifficulty = InheritInt(
            child.PacificationDifficulty,
            parent.PacificationDifficulty,
            Defaults.PacificationDifficulty
        );
        child.ControlSlots = InheritInt(child.ControlSlots, parent.ControlSlots, Defaults.ControlSlots);
        child.CanRun = InheritBool(child.CanRun, parent.CanRun, Defaults.CanRun);
        child.FleesAtHitsPercent = InheritInt(
            child.FleesAtHitsPercent,
            parent.FleesAtHitsPercent,
            Defaults.FleesAtHitsPercent
        );
        child.SpellAttackType = InheritInt(child.SpellAttackType, parent.SpellAttackType, Defaults.SpellAttackType);
        child.SpellAttackDelay = InheritInt(
            child.SpellAttackDelay,
            parent.SpellAttackDelay,
            Defaults.SpellAttackDelay
        );

        if (child.FixedEquipment.Count == 0 && parent.FixedEquipment.Count > 0)
        {
            child.FixedEquipment = parent
                                   .FixedEquipment
                                   .Select(
                                       static equipment => new MobileEquipmentItemTemplate
                                       {
                                           ItemTemplateId = equipment.ItemTemplateId,
                                           Layer = equipment.Layer
                                       }
                                   )
                                   .ToList();
        }

        if (child.RandomEquipment.Count == 0 && parent.RandomEquipment.Count > 0)
        {
            child.RandomEquipment = parent
                                    .RandomEquipment
                                    .Select(
                                        static pool => new MobileRandomEquipmentPoolTemplate
                                        {
                                            Name = pool.Name,
                                            Layer = pool.Layer,
                                            SpawnChance = pool.SpawnChance,
                                            Items = pool
                                                    .Items
                                                    .Select(
                                                        static item => new MobileWeightedEquipmentItemTemplate
                                                        {
                                                            ItemTemplateId = item.ItemTemplateId,
                                                            Weight = item.Weight
                                                        }
                                                    )
                                                    .ToList()
                                        }
                                    )
                                    .ToList();
        }

        child.Params = MergeParams(parent.Params, child.Params);
    }

    private static ItemTemplateParamDefinition CloneParam(ItemTemplateParamDefinition param)
        => new()
        {
            Type = param.Type,
            Value = param.Value
        };

    private static bool InheritBool(bool childValue, bool parentValue, bool defaultValue)
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

    private static void NormalizeTitleAndName(MobileTemplateDefinition template)
    {
        if (string.IsNullOrWhiteSpace(template.Title) && !string.IsNullOrWhiteSpace(template.Name))
        {
            template.Title = template.Name;
        }
    }

    private static void ResolveBaseMobiles(List<MobileTemplateDefinition> templates)
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
        MobileTemplateDefinition template,
        Dictionary<string, MobileTemplateDefinition> byId,
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
                throw new InvalidOperationException($"Circular base_mobile reference detected at '{template.Id}'.");
            }
        }

        states[template.Id] = ResolveState.Visiting;

        if (!string.IsNullOrWhiteSpace(template.BaseMobile))
        {
            if (!byId.TryGetValue(template.BaseMobile, out var parent))
            {
                throw new InvalidOperationException(
                    $"Template '{template.Id}' references unknown base_mobile '{template.BaseMobile}'."
                );
            }

            ResolveTemplate(parent, byId, states);
            ApplyInheritance(parent, template);
        }

        states[template.Id] = ResolveState.Done;
    }
}
