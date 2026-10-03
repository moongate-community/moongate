using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Internal.Prompts;

namespace Moongate.Server.Ultima.Data.Prompts;

/// <summary>
///     The session values of the text prompt.
/// </summary>
public static class PromptSessionKeys
{
    public static readonly SessionKey<PromptState?> State = new("PromptState");
}
