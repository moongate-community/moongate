using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Descriptors;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("command", "Provides command registration APIs for scripts.")]

/// <summary>
/// Exposes command registration helpers to Lua scripts.
/// </summary>
public sealed class CommandModule
{
    private static int _isLuaContextTypeRegistered;
    private readonly ICommandSystemService _commandSystemService;

    public CommandModule(ICommandSystemService commandSystemService)
    {
        _commandSystemService = commandSystemService;
    }

    private readonly record struct CommandRegistrationOptions(
        string Description,
        CommandSourceType Source,
        AccountType MinimumAccountType
    );

    [ScriptFunction("register", "Registers a command handler from Lua.")]
    public void Register(string name, Closure handler, Table? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);

        RegisterLuaContextType();

        var configuration = ParseOptions(options);
        _commandSystemService.RegisterCommand(
            name,
            context =>
            {
                var luaContext = new LuaCommandContext(context);
                try
                {
                    handler.OwnerScript.Call(handler, UserData.Create(luaContext));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Lua command handler failed: {name}",
                        exception
                    );
                }

                return Task.CompletedTask;
            },
            configuration.Description,
            configuration.Source,
            configuration.MinimumAccountType
        );
    }

    private static CommandRegistrationOptions ParseOptions(Table? options)
    {
        if (options is null)
        {
            return new(
                string.Empty,
                CommandSourceType.InGame,
                AccountType.Regular
            );
        }

        var source = ReadSource(options);
        var hasMinimum = TryReadEnum(options, "minimum_account_type", out AccountType minimumAccountType);

        if (!hasMinimum)
        {
            minimumAccountType = source.HasFlag(CommandSourceType.Console)
                                     ? AccountType.Administrator
                                     : AccountType.Regular;
        }

        var description =
            ReadString(options, "description") ??
            ReadString(options, "help_text") ??
            string.Empty;

        return new(description, source, minimumAccountType);
    }

    private static CommandSourceType ReadSource(Table options)
    {
        if (
            !TryReadEnum(options, "source", out CommandSourceType source) &&
            !TryReadEnum(options, "command_source", out source)
        )
        {
            source = CommandSourceType.InGame;
        }

        var normalized = source & (CommandSourceType.InGame | CommandSourceType.Console);

        if (normalized == 0)
        {
            throw new ArgumentException("Invalid command source value.", nameof(options));
        }

        return normalized;
    }

    private static string? ReadString(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.String ? value.String : null;
    }

    private static void RegisterLuaContextType()
    {
        if (Interlocked.CompareExchange(ref _isLuaContextTypeRegistered, 1, 0) != 0)
        {
            return;
        }

        var type = typeof(LuaCommandContext);
        UserData.RegisterType(type, new GenericUserDataDescriptor(type));
    }

    private static bool TryReadEnum<TEnum>(Table table, string key, out TEnum value) where TEnum : struct, Enum
    {
        var optionValue = table.Get(key);

        if (optionValue.Type == DataType.Nil || optionValue.Type == DataType.Void)
        {
            value = default;

            return false;
        }

        if (optionValue.Type == DataType.Number)
        {
            value = (TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(optionValue.Number));

            return true;
        }

        if (optionValue.Type == DataType.String)
        {
            return Enum.TryParse(optionValue.String, true, out value);
        }

        if (optionValue.ToObject() is TEnum enumValue)
        {
            value = enumValue;

            return true;
        }

        value = default;

        return false;
    }
}
