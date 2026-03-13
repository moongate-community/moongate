using System.Globalization;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Utils;
using Moongate.UO.Data.Interfaces.Art;
using Moongate.UO.Data.Interfaces.Templates;
using Serilog;
using SixLabors.ImageSharp.Formats.Png;

namespace Moongate.Server.Services.World;

/// <summary>
/// Exports item art images for loaded item templates into the images/items directory.
/// </summary>
public sealed class ItemsImageBuilder : IWorldGenerator
{
    private readonly ILogger _logger = Log.ForContext<ItemsImageBuilder>();
    private readonly IArtService _artService;
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly IItemTemplateService _itemTemplateService;

    public ItemsImageBuilder(
        IArtService artService,
        IItemTemplateService itemTemplateService,
        DirectoriesConfig directoriesConfig
    )
    {
        _artService = artService;
        _itemTemplateService = itemTemplateService;
        _directoriesConfig = directoriesConfig;
    }

    public string Name => "items_images";

    public Task GenerateAsync(Action<string>? logCallback = null, CancellationToken cancellationToken = default)
    {
        var templates = _itemTemplateService.GetAll();
        var destinationDirectory = Path.Combine(_directoriesConfig[DirectoryType.Images], "items");
        Directory.CreateDirectory(destinationDirectory);

        var generated = 0;
        var skipped = 0;
        var failed = 0;

        logCallback?.Invoke($"Items image export started. Templates: {templates.Count}.");

        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryParseItemId(template.ItemId, out var itemId))
            {
                skipped++;
                logCallback?.Invoke($"Skipping template '{template.Id}': invalid itemId '{template.ItemId}'.");

                continue;
            }

            try
            {
                using var image = _artService.GetArt(itemId);

                if (image is null)
                {
                    skipped++;

                    continue;
                }

                var fileName = $"{SanitizeFileName(template.Id)}_{itemId:X4}.png";
                var outputPath = Path.Combine(destinationDirectory, fileName);
                using var normalized = ItemImageNormalizer.CropAndPad(image);
                using var stream = File.Create(outputPath);
                normalized.Save(stream, new PngEncoder());
                generated++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.Warning(
                    ex,
                    "Failed to export item image for template {TemplateId} with itemId {ItemId}",
                    template.Id,
                    template.ItemId
                );
            }
        }

        var summary = $"Items image export completed. Generated={generated}, Skipped={skipped}, Failed={failed}.";
        logCallback?.Invoke(summary);
        _logger.Information(summary);

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return "unknown";
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var buffer = templateId.ToCharArray();

        for (var i = 0; i < buffer.Length; i++)
        {
            if (invalidCharacters.Contains(buffer[i]))
            {
                buffer[i] = '_';
            }
        }

        return new(buffer);
    }

    private static bool TryParseItemId(string itemIdText, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(itemIdText))
        {
            return false;
        }

        var value = itemIdText.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out itemId
            );
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
    }
}
