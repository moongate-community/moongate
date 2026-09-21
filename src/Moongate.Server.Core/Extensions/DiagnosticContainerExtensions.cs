using DryIoc;
using Moongate.Server.Core.Interfaces.Diagnostics;

namespace Moongate.Server.Core.Extensions;

/// <summary>Registers container-owned singleton metric providers collected by the diagnostic service.</summary>
public static class DiagnosticContainerExtensions
{
    /// <summary>Registers one metric provider as an additional singleton <see cref="IMetricProvider" /> contribution.</summary>
    public static Container AddMetricProvider<TProvider>(this Container container)
        where TProvider : class, IMetricProvider
    {
        ArgumentNullException.ThrowIfNull(container);

        container.Register<IMetricProvider, TProvider>(Reuse.Singleton);

        return container;
    }
}
