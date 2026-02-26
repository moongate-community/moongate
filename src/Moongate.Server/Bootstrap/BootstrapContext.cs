using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Server.Data.Config;
using Moongate.Server.Interfaces.Services.Console;
using Serilog;

namespace Moongate.Server.Bootstrap;

/// <summary>
/// Shared state passed through all bootstrap phases during server initialization.
/// </summary>
public sealed class BootstrapContext
{
    public required Container Container { get; init; }

    public required MoongateConfig Config { get; init; }

    public required IConsoleUiService ConsoleUiService { get; init; }

    public DirectoriesConfig DirectoriesConfig { get; set; } = null!;

    public ILogger Logger { get; set; } = null!;
}
