using DryIoc;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.TestSupport.Ultima.Help;

/// <summary>
///     A <see cref="HelpPageService" /> over the recording pieces the tests use, with the texts the service tells in English.
/// </summary>
public sealed class HelpPageServices : IDisposable
{
    private readonly Container _container = new();

    public RecordingDataAccess<HelpPageEntity> Table { get; } = new();

    public RecordingSpeechService Speech { get; private set; } = new();

    public SettableClock Clock { get; } = new();

    public HelpConfig Config { get; } = new();

    public IMoongateEventBus Events { get; }

    public HelpPageService Service { get; private set; } = null!;

    public HelpPageServices()
    {
        _container.RegisterMoongateEventBus();
        Events = _container.Resolve<IMoongateEventBus>();
    }

    public static HelpPageServices Create(BroadcastFixture fixture, RecordingSpeechService? speech = null)
    {
        var services = new HelpPageServices();

        if (speech is not null)
        {
            services.Speech = speech;
        }

        services.Service = services.Build(fixture);

        return services;
    }

    /// <summary>
    ///     Builds another service over the same table, as a restart of the server does.
    /// </summary>
    public HelpPageService Build(BroadcastFixture fixture)
    {
        return new HelpPageService(
            Table,
            fixture.Mobiles,
            fixture.Sessions,
            Speech,
            Events,
            fixture.Network.Loop,
            Config,
            Clock,
            TestLocalization.With(
                (30207, "Question"),
                (30208, "Bug"),
                (30209, "Suggestion"),
                (30210, "Harassment"),
                (30216, "{0} asks for help ({1}): {2}"),
                (30217, "Game master {0} answers: {1}"),
                (30218, "Help requests waiting: {0}. Type .pages.")
            )
        );
    }

    public void Dispose()
    {
        _container.Dispose();
    }
}
