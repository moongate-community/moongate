using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     <c>unlock</c>: unlocks the door you target, and the door linked to it.
/// </summary>
public sealed class UnlockCommand : ICommandExecutor
{
    private readonly ITargetService _targets;
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public UnlockCommand(
        ITargetService targets,
        IItemService items,
        IItemTemplateService templates,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _items = items;
        _templates = templates;
        _loop = loop;
        _localization = localization;
    }

    public Task ExecuteAsync(CommandContext context)
    {
        return TargetedDoorLock.RunAsync(context, "unlock", false, _targets, _items, _templates, _loop, _localization);
    }
}
