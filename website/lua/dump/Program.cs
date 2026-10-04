using System.Globalization;
using System.Reflection;
using System.Text.Json;
using DryIoc;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Data.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Server.Ultima.Extensions;

namespace Moongate.Website.Lua;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static void Main(string[] args)
    {
        // The world's modules and enums come from the registration the server itself calls. The scripting
        // assembly's own modules are reflected instead: the engine binds engine, timer and events itself and
        // the server registers log, and neither runs without a server.
        using var container = new Container();
        container.AddUltimaScriptModules();
        var registry = container.Resolve<IScriptModuleRegistry>();
        var builtIn = typeof(LogModule).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<ScriptModuleAttribute>() is not null);
        var modules = builtIn.Concat(registry.ModuleTypes)
            .Select(LuaModuleDescriber.Describe)
            .OrderBy(module => module.Name, StringComparer.Ordinal)
            .ToList();

        // As the engine publishes them: the registered enums and every enum a signature or a constant mentions.
        var enums = registry.EnumTypes.Concat(modules.SelectMany(LuaModuleDescriber.EnumsOf))
            .Distinct()
            .OrderBy(type => type.Name, StringComparer.Ordinal);
        var dump = new { modules = modules.Select(ToRow).ToArray(), enums = enums.Select(ToRow).ToArray() };
        File.WriteAllText(args[0], JsonSerializer.Serialize(dump, JsonOptions));
    }

    private static object ToRow(ModuleDescription module)
    {
        var assembly = module.ModuleType.Assembly.GetName().Name!;
        var namespaceTail = module.ModuleType.Namespace![assembly.Length..].TrimStart('.').Replace('.', '/');
        var source = $"src/{assembly}/{namespaceTail}/{module.ModuleType.Name}.cs";

        // The page links to this file; run from the repository root, as the site build does.
        if (!File.Exists(source))
        {
            throw new InvalidOperationException($"Module {module.Name}: {source} does not exist.");
        }

        return new
        {
            name = module.Name,
            description = module.HelpText,
            source,
            functions = module.Functions
                .Select(LuaFunctionDescriber.Describe)
                .OrderBy(function => function.LuaName, StringComparer.Ordinal)
                .Select(function => new
                {
                    name = function.LuaName,
                    help = function.HelpText,
                    parameters = function.Parameters.Select(parameter => new
                    {
                        name = parameter.Name,
                        type = parameter.LuaType,
                        optional = parameter.Optional,
                        @default = parameter.Default
                    }),
                    returns = function.Returns
                }),
            constants = module.Constants
                .OrderBy(constant => constant.LuaName, StringComparer.Ordinal)
                .Select(constant => new
                {
                    name = constant.LuaName,
                    type = LuaFunctionDescriber.LuaTypeName(constant.Type),
                    value = LuaFunctionDescriber.LuaLiteral(constant.Value, constant.Type),
                    help = constant.HelpText
                })
        };
    }

    private static object ToRow(Type enumType)
    {
        return new
        {
            name = enumType.Name,
            members = Enum.GetNames(enumType).Select(member => new
            {
                name = member,
                value = Convert.ToInt64(Enum.Parse(enumType, member), CultureInfo.InvariantCulture)
            })
        };
    }
}
