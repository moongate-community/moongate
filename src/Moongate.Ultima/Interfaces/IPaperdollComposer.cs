using Moongate.Ultima.Data;

namespace Moongate.Ultima.Interfaces;

/// <summary>
/// Composites a full paperdoll PNG from client gump art. Requires the client directory
/// to be set via <c>Files.SetDirectory</c> before use.
/// </summary>
public interface IPaperdollComposer
{
    /// <summary>The composed 260x237 PNG, or null when the body gump is missing.</summary>
    Stream? Compose(PaperdollRequest request);
}
