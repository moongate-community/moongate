using System.Threading.Channels;
using DryIoc;
using Moongate.Server.Core.Data.Diagnostics;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Services.Diagnostics;
using Moongate.Server.Services.Events;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class DiagnosticServiceFixture : IDisposable
{
    private readonly Container _container;
    private readonly IDisposable _subscription;
    private readonly Channel<DiagnosticSnapshot> _snapshots;

    public DiagnosticTimeProvider Time { get; }
    public EventBusService Bus { get; }
    public DiagnosticService Service { get; }

    public bool HasPendingSnapshot => _snapshots.Reader.TryPeek(out _);

    public DiagnosticServiceFixture(IEnumerable<IMetricProvider> providers, DiagnosticOptions? options = null)
    {
        _container = new();
        _container.RegisterMoongateEventBus();
        Bus = new(_container.Resolve<IMoongateEventBus>());
        Time = new();
        Service = new(providers, options ?? new DiagnosticOptions(), Bus, Time);
        _snapshots = Channel.CreateUnbounded<DiagnosticSnapshot>();
        _subscription = Bus.Subscribe<DiagnosticSnapshotCollectedEvent>((message, _) =>
            {
                _snapshots.Writer.TryWrite(message.Snapshot);

                return Task.CompletedTask;
            }
        );
    }

    public Task<DiagnosticSnapshot> NextAsync()
    {
        return _snapshots.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        try
        {
            Service.Dispose();
        }
        finally
        {
            Time.Dispose();
            _subscription.Dispose();
            _container.Dispose();
        }
    }
}
