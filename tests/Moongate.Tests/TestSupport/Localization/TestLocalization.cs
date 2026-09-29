using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Messages;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.TestSupport.Localization;

/// <summary>
///     A real <see cref="ILocalizationService" /> over the given message texts, keyed by id.
/// </summary>
public static class TestLocalization
{
    public static ILocalizationService With(params (int Id, string Text)[] messages)
    {
        var data = new StubDataLoaderService().With(
            messages.Select(message => new MessageContent { Id = message.Id, Text = message.Text }).ToArray()
        );

        return new LocalizationService(new LocalizationConfig { Language = "ita" }, data);
    }
}
