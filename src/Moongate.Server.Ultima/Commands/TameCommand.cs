using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Gives the creature the game master targets to an owner: the game master, or the character named in the arguments.
///     Only a creature that can be ridden is given; the owner is the one who may ride it.
/// </summary>
public sealed class TameCommand : ICommandExecutor
{
    private readonly ITargetService _targets;
    private readonly IMobileService _mobiles;
    private readonly IMobileTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public TameCommand(
        ITargetService targets,
        IMobileService mobiles,
        IMobileTemplateService templates,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _targets = targets;
        _mobiles = mobiles;
        _templates = templates;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("tame works in game only.");

            return;
        }

        var target = await _targets.RequestAsync(
            session,
            TargetCursorType.Object,
            TargetFlagsType.Neutral,
            context.CancellationToken
        );

        if (target.Kind != TargetResultType.Object)
        {
            context.Print(_localization.Text(CommandMessages.TargetCanceled, "Target canceled."));

            return;
        }

        var name = string.Join(' ', context.Arguments);

        // On the game loop, where the mobiles live.
        var answer = "";
        var failed = false;
        var work = new LoopActionWorkItem(() =>
            {
                if (!_mobiles.TryGet(target.Serial, out var creature) || !_mobiles.IsInWorld(creature.Id))
                {
                    answer = _localization.Text(CommandMessages.NotAnNpc, "That is not an NPC.");
                    failed = true;

                    return;
                }

                if (!IsMount(creature))
                {
                    answer = _localization.Text(CommandMessages.CannotTame, "{0} cannot be tamed.", creature.Name ?? "");
                    failed = true;

                    return;
                }

                var owner = OwnerOf(session.CharacterId, name);

                if (owner is null)
                {
                    answer = _localization.Text(CommandMessages.JailNobodyNamed, "No character is named {0}.", name);
                    failed = true;

                    return;
                }

                creature.SetProp(MountProps.Owner, (long)owner.Id.Value);
                answer = _localization.Text(
                    CommandMessages.Tamed,
                    "{0} now belongs to {1}.",
                    creature.Name ?? "",
                    owner.Name ?? ""
                );
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (failed)
        {
            context.PrintError(answer);

            return;
        }

        context.Print(answer);
    }

    private bool IsMount(MobileEntity creature)
    {
        return creature.IsNpc &&
               creature.TemplateId is { } id &&
               _templates.TryGet(id, out var template) &&
               template.MountItem() is not null;
    }

    // The game master itself with no name; otherwise the player of that name, whatever its case.
    private MobileEntity? OwnerOf(Moongate.Core.Primitives.Serial self, string name)
    {
        if (name.Length == 0)
        {
            return _mobiles.TryGet(self, out var master) ? master : null;
        }

        return _mobiles.Mobiles.FirstOrDefault(mobile =>
            !mobile.IsNpc && string.Equals(mobile.Name, name, StringComparison.OrdinalIgnoreCase)
        );
    }
}
