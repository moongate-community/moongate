using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Http.Plugin.Extensions;
using Moongate.Core.Types;
using Moongate.News.Plugin.Commands;
using Moongate.News.Plugin.Endpoints;
using Moongate.News.Plugin.Entities;
using Moongate.News.Plugin.Interfaces;
using Moongate.News.Plugin.Services;
using Moongate.News.Plugin.Subscribers;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Types;
using Moongate.Persistence.Generators;
using SquidStd.Core.Utils;
using SquidStd.Persistence.Extensions;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.News.Plugin;

/// <summary>Registers the shard-news feature: its persisted entity, service and REST endpoints.</summary>
public sealed class MoongateNewsPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.news.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateNewsPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate News",
            Description = "Shard news managed over REST"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterPersistedEntity<NewsEntity, Serial>(
            "news",
            1,
            entity => entity.Id,
            (entity, id) => entity.Id = id,
            new DefaultSerialGenerator()
        );

        container.Register<INewsService, NewsService>(Reuse.Singleton);
        container.RegisterApiEndpoint<NewsAdminEndpoints>();
        container.RegisterApiEndpoint<NewsEndpoints>();

        // The in-game half: a greeting on the way in, and the archive on demand. Both live here
        // rather than in the server, which cannot reference a plugin.
        container.RegisterEventSubscriber<NewsMotdSubscriber>();
        container.RegisterCommand<NewsCommand>(
            "news",
            AccountLevelType.Player,
            "Shows the shard's published news.",
            CommandSourceType.InGame | CommandSourceType.Console | CommandSourceType.Rest
        );
    }
}
