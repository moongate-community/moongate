using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

[RegisterConsoleCommand(
    "reload_template|reload_templates",
    "Reload all templates from disk.",
    CommandSourceType.InGame | CommandSourceType.Console,
    AccountType.GameMaster
)]
public sealed class ReloadTemplateCommand : ICommandExecutor
{
    private readonly IFileLoaderService _fileLoaderService;
    private readonly IItemTemplateService _itemTemplateService;
    private readonly IMobileTemplateService _mobileTemplateService;

    public ReloadTemplateCommand(
        IFileLoaderService fileLoaderService,
        IItemTemplateService itemTemplateService,
        IMobileTemplateService mobileTemplateService
    )
    {
        _fileLoaderService = fileLoaderService;
        _itemTemplateService = itemTemplateService;
        _mobileTemplateService = mobileTemplateService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length > 1)
        {
            context.Print("Usage: reload_template [filePath]");

            return;
        }

        try
        {
            if (context.Arguments.Length == 1)
            {
                var filePath = context.Arguments[0];
                await _fileLoaderService.LoadSingleAsync(filePath);
                context.Print("Template reloaded successfully: {0}.", filePath);

                return;
            }

            await _fileLoaderService.ExecuteLoadersAsync();
            context.Print(
                "Templates reloaded successfully. ItemTemplates={0}, MobileTemplates={1}.",
                _itemTemplateService.Count,
                _mobileTemplateService.Count
            );
        }
        catch (Exception ex)
        {
            if (context.Arguments.Length == 1)
            {
                context.PrintError("Failed to reload template {0}: {1}", context.Arguments[0], ex.Message);

                return;
            }

            context.PrintError("Failed to reload templates: {0}", ex.Message);
        }
    }
}
