using Moongate.Core.Primitives;

namespace Moongate.Server.Abstractions.Interfaces.Items;

/// <summary>
/// Remembers which mobiles have which containers open, so an item inside one can be redrawn for the
/// players actually looking at it. There is no close packet worth trusting, so entries are dropped
/// when the opener walks out of range — the same lazy pruning ModernUO does.
/// </summary>
public interface IContainerOpenerRegistry
{
    /// <summary>Drops <paramref name="mobile" /> from <paramref name="container" />'s openers.</summary>
    void Closed(Serial container, Serial mobile);

    /// <summary>Drops <paramref name="mobile" /> from every container it had open.</summary>
    void ForgetMobile(Serial mobile);

    /// <summary>Records that <paramref name="mobile" /> is looking inside <paramref name="container" />.</summary>
    void Opened(Serial container, Serial mobile);

    /// <summary>The mobiles believed to have <paramref name="container" /> open. Never null.</summary>
    IReadOnlyCollection<Serial> OpenersOf(Serial container);
}
