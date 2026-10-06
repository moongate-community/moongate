using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Speech;
using Serilog;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     Runs the server's commands from a script: as the console, or as a player wrote them.
/// </summary>
/// <remarks>
///     A command runs detached from the script that asks for it, as one typed in game does: the function answers
///     whether it was started, not what it did.
/// </remarks>
[ScriptModule("commands", "Runs the commands of the server, as its console or as a player.")]
public sealed class CommandsModule
{
    private static readonly Hue InformationHue = new(0x03B2);
    private static readonly Hue WarningHue = new(0x0035);
    private static readonly Hue ErrorHue = new(0x0021);

    private readonly ILogger _logger = Log.ForContext<CommandsModule>();
    private readonly ICommandSystemService _commands;
    private readonly ISessionService _sessions;
    private readonly IPacketSendService _sender;

    public CommandsModule(ICommandSystemService commands, ISessionService sessions, IPacketSendService sender)
    {
        _commands = commands;
        _sessions = sessions;
        _sender = sender;
    }

    /// <summary>
    ///     Runs a command as the server console; <c>commands.execute("season", "winter")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Runs a command as the server console does: with every power and with no player behind it, so a command that needs one, such as a target cursor, answers that it works in game only. The arguments follow the name, one each: strings, numbers and booleans, joined by spaces into one line (an argument with a space in it is read as two). True when the command was started, false for an empty name, a line end in it or in an argument, or an argument that is a table or a function. The command runs on its own: what it answers is written in the server log, and a failure of it is logged, never raised in the script."
    )]
    public bool Execute(string command, params object?[] args)
    {
        if (!TryLine(command, args, out var line))
        {
            return false;
        }

        _ = RunAsync(line, CommandSourceType.Console, null);

        return true;
    }

    /// <summary>
    ///     Runs a command as a player wrote it; <c>commands.execute_as(player, "go", "britain")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Runs a command as the player wrote it in game: with the level of its account, so a command above it is refused as it would be, and with its session, so a target cursor opens for it. The player reads what the command answers. True when the command was started; false as commands.execute, and for a player that is not in the world. The command runs on its own."
    )]
    public bool ExecuteAs(long player, string command, params object?[] args)
    {
        if (player is <= 0 or > uint.MaxValue ||
            !TryLine(command, args, out var line) ||
            !_sessions.TryGetByCharacterId(new Serial((uint)player), out var session))
        {
            return false;
        }

        _ = RunAsync(line, CommandSourceType.InGame, session);

        return true;
    }

    // The name and its arguments as the one line a command is typed in.
    private static bool TryLine(string command, object?[] args, out string line)
    {
        line = "";

        if (string.IsNullOrWhiteSpace(command) || command.Any(char.IsControl))
        {
            return false;
        }

        var parts = new List<string>(args.Length + 1) { command.Trim() };

        foreach (var arg in args)
        {
            var text = arg switch
            {
                string value       => value,
                bool value         => value ? "true" : "false",
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                _                  => null
            };

            if (text is null || text.Any(char.IsControl))
            {
                return false;
            }

            parts.Add(text);
        }

        line = string.Join(' ', parts);

        return true;
    }

    // Detached from the script, as a command typed in game is from its packet: it may wait for a player.
    private async Task RunAsync(string line, CommandSourceType source, GameSession? session)
    {
        try
        {
            var output = await _commands.ExecuteAsync(line, source, session, CancellationToken.None);

            foreach (var answer in output)
            {
                if (session is null)
                {
                    _logger.Information("Command {Command} from a script: {Answer}", line, answer.Text);
                }
                else if (!_sender.TrySend(session.SessionId, SpeechMessageHelper.CreateSystem(answer.Text, HueOf(answer))))
                {
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Command {Command} from a script failed", line);
        }
    }

    private static Hue HueOf(CommandOutputLine answer)
    {
        return answer.Level switch
        {
            CommandOutputLevel.Warning => WarningHue,
            CommandOutputLevel.Error   => ErrorHue,
            _                          => InformationHue
        };
    }
}
