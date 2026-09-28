using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Motd;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Server.Ultima.Speech;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>Loads and validates the optional MOTD before characters may enter the world.</summary>
public sealed class MotdLoader : IDataLoader<MotdLine>
{
    private readonly DirectoriesConfig _directories;
    private readonly IMotdVariableRegistry _variables;
    private readonly ILogger _logger = Log.ForContext<MotdLoader>();

    private string FilePath => Path.Combine(_directories["data"], "motd.toml");

    public MotdLoader(DirectoriesConfig directories, IMotdVariableRegistry variables)
    {
        _directories = directories;
        _variables = variables;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MotdLine>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        _variables.Freeze();
        var path = FilePath;

        if (!File.Exists(path))
        {
            _logger.Warning("MOTD file {Path} is missing; no MOTD will be sent", path);

            return new() { Entities = [] };
        }

        MotdFile file;

        try
        {
            file = await TomlUtils.DeserializeFromFileAsync<MotdFile>(path, null, cancellationToken) ?? new MotdFile();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException($"{path}: invalid MOTD TOML.", exception);
        }

        if (file.Lines is null)
        {
            throw new InvalidDataException($"{path}: 'lines' must be an array of strings.");
        }

        var lines = new List<MotdLine>();

        for (var index = 0; index < file.Lines.Count; index++)
        {
            var template = file.Lines[index];
            var where = $"{path}: line {index + 1}";

            if (template is null)
            {
                throw new InvalidDataException($"{where} must be a string.");
            }

            if (template.Contains('\0'))
            {
                throw new InvalidDataException($"{where} contains NUL.");
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                continue;
            }

            try
            {
                _ = SpeechMessageHelper.CreateSystem(template, new Hue(0x03B2));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"{where} is not a valid Unicode speech line.", exception);
            }

            foreach (System.Text.RegularExpressions.Match match in MotdTemplateTokens.Find(template))
            {
                var name = match.Groups[1].Value;

                if (!MotdTemplateTokens.IsValidName(name))
                {
                    throw new InvalidDataException($"{where} has invalid variable '{name}'.");
                }

                if (!_variables.Contains(name))
                {
                    throw new InvalidDataException($"{where} uses unknown variable '{name}'.");
                }
            }

            lines.Add(new MotdLine(index + 1, template));
        }

        _logger.Information("Loaded {Count} MOTD lines from {Path}", lines.Count, path);

        return new() { Entities = lines };
    }
}
