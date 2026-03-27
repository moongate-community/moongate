using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Moongate.Scripting.Data.Scripts;
using MoonSharp.Interpreter;
using Serilog;
using SyntaxErrorException = System.Data.SyntaxErrorException;

namespace Moongate.Scripting.Internal;

/// <summary>
/// Reflection and MoonSharp interop helpers used by the Lua script engine.
/// </summary>
internal static class LuaReflectionHelper
{
    public static object?[] ConvertArgumentsToArray(CallbackArguments args)
    {
        if (args.Count == 0)
        {
            return Array.Empty<object?>();
        }

        var converted = new object?[args.Count];

        for (var i = 0; i < args.Count; i++)
        {
            converted[i] = args[i].ToObject();
        }

        return converted;
    }

    public static object? ConvertFromLua(DynValue dynValue, Type targetType)
        => dynValue.Type switch
        {
            DataType.Nil     => null,
            DataType.Boolean => dynValue.Boolean,
            DataType.Number  => Convert.ChangeType(
                dynValue.Number,
                GetConversionTargetType(targetType),
                CultureInfo.InvariantCulture
            ),
            DataType.String  => dynValue.String,
            DataType.Table   => dynValue.ToObject(),
            _                => dynValue.ToObject()
        };

    public static DynValue ConvertToLua(Script script, object? value)
    {
        ArgumentNullException.ThrowIfNull(script);

        return value == null ? DynValue.Nil : DynValue.FromObject(script, value);
    }

    public static Func<object?, object?, object?, object?, object?> CreateConstructorWrapper(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    )
    {
        ArgumentNullException.ThrowIfNull(type);

        var constructorsByParamCount = new Dictionary<int, ConstructorInfo>();
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        foreach (var ctor in constructors)
        {
            var paramCount = ctor.GetParameters().Length;
            constructorsByParamCount.TryAdd(paramCount, ctor);
        }

        return (arg1, arg2, arg3, arg4) =>
               {
                   var rawArgs = new List<object?>();

                   if (arg1 != null)
                   {
                       rawArgs.Add(arg1);
                   }

                   if (arg2 != null)
                   {
                       rawArgs.Add(arg2);
                   }

                   if (arg3 != null)
                   {
                       rawArgs.Add(arg3);
                   }

                   if (arg4 != null)
                   {
                       rawArgs.Add(arg4);
                   }

                   var argCount = rawArgs.Count;

                   if (constructorsByParamCount.TryGetValue(argCount, out var ctor))
                   {
                       try
                       {
                           var parameters = ctor.GetParameters();
                           var convertedArgs = new object?[argCount];

                           for (var i = 0; i < argCount; i++)
                           {
                               var parameterType = parameters[i].ParameterType;
                               var argumentValue = rawArgs[i];

                               if (argumentValue == null)
                               {
                                   convertedArgs[i] = null;
                               }
                               else if (parameterType.IsInstanceOfType(argumentValue))
                               {
                                   convertedArgs[i] = argumentValue;
                               }
                               else
                               {
                                   try
                                   {
                                       convertedArgs[i] = Convert.ChangeType(
                                           argumentValue,
                                           parameterType,
                                           CultureInfo.InvariantCulture
                                       );
                                   }
                                   catch
                                   {
                                       convertedArgs[i] = argumentValue;
                                   }
                               }
                           }

                           return Activator.CreateInstance(type, convertedArgs);
                       }
                       catch (Exception ex)
                       {
                           throw new ScriptRuntimeException(
                               $"Constructor of {type.Name} with {argCount} arguments failed: {ex.Message}",
                               ex
                           );
                       }
                   }

                   var availableConstructors = string.Join(", ", constructorsByParamCount.Keys.OrderBy(key => key));

                   throw new ScriptRuntimeException(
                       $"No constructor found for {type.Name} with {argCount} arguments. Available: {availableConstructors}"
                   );
               };
    }

    public static ScriptErrorInfo CreateErrorInfo(
        ScriptRuntimeException luaException,
        string sourceCode,
        string? fileName = null
    )
    {
        ArgumentNullException.ThrowIfNull(luaException);
        ArgumentNullException.ThrowIfNull(sourceCode);

        return new()
        {
            Message = luaException.DecoratedMessage ?? luaException.Message,
            StackTrace = luaException.StackTrace,
            LineNumber = 0,
            ColumnNumber = 0,
            ErrorType = "LuaError",
            SourceCode = sourceCode,
            FileName = fileName ?? "script.lua"
        };
    }

    public static ScriptErrorInfo CreateErrorInfo(
        InterpreterException luaException,
        string sourceCode,
        string? fileName = null
    )
    {
        ArgumentNullException.ThrowIfNull(luaException);
        ArgumentNullException.ThrowIfNull(sourceCode);

        int? lineNumber = null;
        int? columnNumber = null;
        var errorType = "LuaError";

        if (luaException is SyntaxErrorException)
        {
            errorType = "SyntaxError";
        }

        var message = luaException.Message;

        if (message.Contains('('))
        {
            var match = Regex.Match(message, @"\((\d+),(\d+)");

            if (match.Success)
            {
                lineNumber = int.Parse(match.Groups[1].Value, CultureInfo.CurrentCulture);
                columnNumber = int.Parse(match.Groups[2].Value, CultureInfo.CurrentCulture);
            }
        }

        return new()
        {
            Message = luaException.DecoratedMessage ?? luaException.Message,
            StackTrace = luaException.StackTrace,
            LineNumber = lineNumber,
            ColumnNumber = columnNumber,
            ErrorType = errorType,
            SourceCode = sourceCode,
            FileName = fileName ?? "script.lua"
        };
    }

    public static DynValue CreateMethodClosure(ILogger logger, Script script, object instance, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(method);

        return DynValue.NewCallback(
            (_, args) =>
            {
                try
                {
                    var parameters = method.GetParameters();
                    var hasParamsArray = parameters.Length > 0 &&
                                         parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);

                    object?[] convertedArgs;

                    if (hasParamsArray)
                    {
                        var regularParamsCount = parameters.Length - 1;
                        convertedArgs = new object?[parameters.Length];

                        for (var i = 0; i < regularParamsCount && i < args.Count; i++)
                        {
                            convertedArgs[i] = ConvertFromLua(args[i], parameters[i].ParameterType);
                        }

                        var paramsArrayType = parameters[^1].ParameterType.GetElementType()!;
                        var paramsCount = Math.Max(0, args.Count - regularParamsCount);
                        var paramsArray = Array.CreateInstance(paramsArrayType, paramsCount);

                        for (var i = 0; i < paramsCount; i++)
                        {
                            var argumentIndex = regularParamsCount + i;
                            paramsArray.SetValue(ConvertFromLua(args[argumentIndex], paramsArrayType), i);
                        }

                        convertedArgs[^1] = paramsArray;
                    }
                    else
                    {
                        convertedArgs = new object?[parameters.Length];

                        for (var i = 0; i < parameters.Length && i < args.Count; i++)
                        {
                            convertedArgs[i] = ConvertFromLua(args[i], parameters[i].ParameterType);
                        }
                    }

                    var result = method.Invoke(instance, convertedArgs);

                    return method.ReturnType == typeof(void) ? DynValue.Nil : ConvertToLua(script, result);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error calling method {MethodName}", method.Name);

                    throw new ScriptRuntimeException(ex.Message);
                }
            }
        );
    }

    public static Table ObjectToTable(Script script, object obj)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(obj);

        var table = new Table(script);
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            table[property.Name] = value;
        }

        return table;
    }

    public static TInput? PrepareManualInput<TInput>(CallbackArguments args)
    {
        if (typeof(TInput) == typeof(object[]))
        {
            return (TInput?)(object?)ConvertArgumentsToArray(args);
        }

        if (args.Count == 0)
        {
            return default;
        }

        var firstArg = args[0];
        var converted = ConvertFromLua(firstArg, typeof(TInput));

        return converted is null ? default : (TInput?)converted;
    }

    private static Type GetConversionTargetType(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        return Nullable.GetUnderlyingType(targetType) ?? targetType;
    }
}
