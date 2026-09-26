using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Reads the messages <see cref="Loaders.MessagesLoader" /> loaded, the first time a text is asked for, so it can be
///     created before <see cref="IDataLoaderService" /> has started.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly LocalizationConfig _localizationConfig;

    private readonly Lazy<FrozenDictionary<int, string>> _messages;

    public string Language => _localizationConfig.Language.ToLowerInvariant();

    public LocalizationService(LocalizationConfig localizationConfig, IDataLoaderService dataLoaderService)
    {
        _localizationConfig = localizationConfig;
        _messages = new(
            () => dataLoaderService.GetEntities<MessageContent>().ToFrozenDictionary(message => message.Id, message => message.Text)
        );
    }

    public string Get(int id, params object[] values)
    {
        if (!TryGetText(id, out var text))
        {
            throw new KeyNotFoundException($"No message has id {id}.");
        }

        return string.Format(CultureInfo.InvariantCulture, text, values);
    }

    public bool TryGetText(int id, [MaybeNullWhen(false)] out string text)
    {
        return _messages.Value.TryGetValue(id, out text);
    }
}
