using ConsoleAppFramework;
using Moongate.Boot.Internal;

await ConsoleApp.RunAsync(args, BootCommand.RunAsync);

// The framework shows help for an empty command line; retain mgboot's usage-error exit code.
if (args.Length == 0)
{
    Environment.ExitCode = 2;
}
