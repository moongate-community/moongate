using ConsoleAppFramework;
using Serilog;

await ConsoleApp.RunAsync(
    args,
    () =>
    {


        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

        Log.Logger.Information("TERM is {Term}", Environment.GetEnvironmentVariable("TERM") );
        Log.Logger.Information("Starting up");
        Log.Logger.Error("Error");
    }
);
