using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Commands.Internal;
using Moongate.Server.Ultima.Data.Decorations;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Speech;
using Serilog;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Places the world decoration of <c>templates/decorations</c>: <c>decorate</c>. In game every file is reported as
///     it is done; the server log has the same lines, and the totals end the output.
/// </summary>
public sealed class DecorateCommand : ICommandExecutor
{
    private static readonly Hue ProgressHue = new(0x03B2);

    private readonly ILogger _logger = Log.ForContext<DecorateCommand>();
    private readonly IDecorationService _decorations;
    private readonly IPacketSendService _sender;
    private readonly ILocalizationService? _localization;

    public DecorateCommand(
        IDecorationService decorations,
        IPacketSendService sender,
        ILocalizationService? localization = null
    )
    {
        _localization = localization;
        _decorations = decorations;
        _sender = sender;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", "decorate"));

            return;
        }

        if (_decorations.IsRunning)
        {
            context.PrintError(_localization.Text(CommandMessages.DecorationRunning, "A decoration is already running."));

            return;
        }

        var progress = context.Session is { } session
            ? new ActionProgress<DecorationFileResult>(
                file => SpeechMessageHelper.TrySend(_sender, session, SpeechMessageHelper.CreateSystem(FileLine(file), ProgressHue))
            )
            : null;

        try
        {
            var result = await _decorations.DecorateAsync(progress, context.CancellationToken);
            context.Print(
                _localization.Text(
                    CommandMessages.DecorationDone,
                    "Decoration done: {0} placed, {1} already there, {2} skipped in {3} files.",
                    result.Placed,
                    result.Present,
                    result.Skipped,
                    result.Files
                )
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The exception is English and technical: the administrator gets the reason from the log.
            _logger.Error(exception, "Decorating the world failed");
            context.PrintError(_localization.Text(CommandMessages.DecorationFailed, "The decoration failed. Check the server logs."));
        }
    }

    private string FileLine(DecorationFileResult file)
    {
        // The kinds keep ModernUO's names in every language.
        var kinds = file.Skipped == 0
            ? string.Empty
            : " (" + string.Join(
                ", ",
                file.SkippedByType.OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key} {pair.Value}")
            ) + ")";

        return _localization.Text(
            CommandMessages.DecoratingFile,
            "Decorating {0}/{1}: {2} placed, {3} already there, {4} skipped{5}.",
            file.Folder,
            file.Name,
            file.Placed,
            file.Present,
            file.Skipped,
            kinds
        );
    }
}
