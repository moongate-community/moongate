using ConsoleAppFramework;
using Moongate.Ctl.Internal;

var arguments = CommandLine.Read(args, out var error);

if (arguments is null)
{
    Console.Error.WriteLine($"mgctl: {error}");
    Console.Error.WriteLine("Run mgctl --help for the commands.");

    return 2;
}

var app = ConsoleApp.Create();
app.Add("init", InitCommand.RunAsync);
app.Add("migrate status", MigrateCommands.StatusAsync);
app.Add("migrate apply", MigrateCommands.ApplyAsync);
app.Add("convert uox", ConvertCommands.Uox);
app.Add("convert modernuo-spawns", ConvertCommands.ModernUoSpawns);
app.Add("convert modernuo-signs", ConvertCommands.ModernUoSigns);
app.Add("convert modernuo-teleporters", ConvertCommands.ModernUoTeleporters);
await app.RunAsync(arguments);

// The framework shows help for an empty command line; retain mgctl's usage-error exit code.
return arguments.Length == 0 ? 2 : Environment.ExitCode;
