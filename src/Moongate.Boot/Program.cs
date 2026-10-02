using ConsoleAppFramework;
using Moongate.Boot.Internal;

// "mgboot <root>" is the older spelling of "mgboot init <root>".
string[] commands = ["init", "migrate", "convert"];

if (args.Length > 0 && !args[0].StartsWith('-') && !commands.Contains(args[0]))
{
    args = ["init", ..args];
}

var app = ConsoleApp.Create();
app.Add("init", BootCommand.RunAsync);
app.Add("migrate status", MigrateCommands.StatusAsync);
app.Add("migrate apply", MigrateCommands.ApplyAsync);
app.Add("convert uox", ConvertCommands.Uox);
app.Add("convert modernuo-spawns", ConvertCommands.ModernUoSpawns);
app.Add("convert modernuo-signs", ConvertCommands.ModernUoSigns);
app.Add("convert modernuo-teleporters", ConvertCommands.ModernUoTeleporters);
await app.RunAsync(args);

// The framework shows help for an empty command line; retain mgboot's usage-error exit code.
if (args.Length == 0)
{
    Environment.ExitCode = 2;
}
