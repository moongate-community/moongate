using Moongate.Server.Interfaces.Services.Scripting;

namespace Moongate.Server.Services.Scripting.Jobs;

public sealed class EchoAsyncLuaJobHandler : IAsyncLuaJobHandler
{
    public string Name => "echo";

    public Task<Dictionary<string, object?>> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        return Task.FromResult(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["payload"] = new Dictionary<string, object?>(payload, StringComparer.Ordinal)
            }
        );
    }
}
