using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Types.Commands;

namespace Moongate.Server.Commands;

/// <summary>
/// Builds all item images from art assets.
/// </summary>
[RegisterConsoleCommand(
    "build_item_images|.build_item_images",
    "Generate item art images into images/items. Usage: .build_item_images",
    CommandSourceType.Console | CommandSourceType.InGame
)]
public sealed class BuildItemImagesCommand : ICommandExecutor
{
    private readonly IWorldGeneratorBuilderService _worldGeneratorBuilderService;

    public BuildItemImagesCommand(IWorldGeneratorBuilderService worldGeneratorBuilderService)
    {
        _worldGeneratorBuilderService = worldGeneratorBuilderService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length > 0)
        {
            context.Print("Usage: .build_item_images");

            return;
        }

        try
        {
            await _worldGeneratorBuilderService.GenerateAsync(
                "items_images",
                message => context.Print("{0}", message)
            );
            context.Print("Items image generation finished.");
        }
        catch (Exception ex)
        {
            context.Print("Items image generation failed: {0}", ex.Message);
        }
    }
}
