using System.Globalization;
using Moongate.Core.Directories;
using Moongate.Core.Utils;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Serilog;

namespace Moongate.Server.Ultima.Loaders;

/// <summary>
///     Loads the texts of <c>data/messages/eng.toml</c> and replaces them with those of the configured language's file.
///     A number that is not a message id, a text that is not a valid composite format, a translation that needs more
///     values than the English text or a translated id missing from English stops the server at startup.
/// </summary>
public class MessagesLoader : IDataLoader<MessageContent>
{
    private const string EnglishLanguage = "eng";

    private const int MaxValues = 16;

    private static readonly object[] SampleValues = Enumerable.Repeat<object>(0, MaxValues).ToArray();

    private readonly DirectoriesConfig _directoriesConfig;

    private readonly LocalizationConfig _localizationConfig;

    private readonly ILogger _logger = Log.ForContext<MessagesLoader>();

    private string language => _localizationConfig.Language.ToLowerInvariant();

    public MessagesLoader(DirectoriesConfig directoriesConfig, LocalizationConfig localizationConfig)
    {
        _directoriesConfig = directoriesConfig;
        _localizationConfig = localizationConfig;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var path in new[] { GetPath(EnglishLanguage), GetPath(language) })
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Messages file {Path.GetFileName(path)} not found", path);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<DataLoaderResult<MessageContent>> LoadDataAsync(CancellationToken cancellationToken = default)
    {
        var english = await ReadAsync(GetPath(EnglishLanguage), cancellationToken);

        if (english.Count == 0)
        {
            throw new InvalidDataException($"{GetPath(EnglishLanguage)} has no [messages] entries.");
        }

        var messages = new Dictionary<int, string>(english);
        var translated = new Dictionary<int, string>();

        if (language != EnglishLanguage)
        {
            var path = GetPath(language);
            translated = await ReadAsync(path, cancellationToken);

            foreach (var (id, text) in translated)
            {
                if (!english.TryGetValue(id, out var englishText))
                {
                    throw new InvalidDataException($"{path}: message {id} is not in {EnglishLanguage}.toml.");
                }

                if (CountValues(text) > CountValues(englishText))
                {
                    throw new InvalidDataException(
                        $"{path}: message {id} needs more values than the English text '{englishText}'."
                    );
                }

                messages[id] = text;
            }
        }

        _logger.Information(
            "Found {Count} messages in {Language}, {Fallback} of them in English",
            messages.Count,
            language,
            language == EnglishLanguage ? 0 : messages.Count - translated.Count
        );

        return new DataLoaderResult<MessageContent>()
        {
            Entities = messages.OrderBy(message => message.Key)
                               .Select(message => new MessageContent { Id = message.Key, Text = message.Value })
                               .ToList()
        };
    }

    private string GetPath(string languageCode)
    {
        return Path.Join(_directoriesConfig["data"], "messages", $"{languageCode}.toml");
    }

    private static async Task<Dictionary<int, string>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var file = await TomlUtils.DeserializeFromFileAsync<MessageContentFile>(path, null, cancellationToken);
        var messages = new Dictionary<int, string>();

        foreach (var (key, text) in file?.Messages ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                throw new InvalidDataException($"{path}: '{key}' is not a message number.");
            }

            if (string.IsNullOrEmpty(text) || CountValues(text) < 0)
            {
                throw new InvalidDataException(
                    $"{path}: message {id} must be a text with at most {MaxValues} values, written {{0}}, {{1}}, ..."
                );
            }

            messages[id] = text;
        }

        return messages;
    }

    /// <summary>
    ///     Returns how many values the text needs to be formatted, or -1 when it is not a valid composite format.
    /// </summary>
    private static int CountValues(string text)
    {
        for (var count = 0; count <= MaxValues; count++)
        {
            try
            {
                _ = string.Format(CultureInfo.InvariantCulture, text, SampleValues[..count]);

                return count;
            }
            catch (FormatException)
            {
            }
        }

        return -1;
    }
}
