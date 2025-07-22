using Jint;
using Jint.Native;
using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;

namespace Moongate.Server.Wraps;

public class AiBrainFuncWrap : IAiBrainAction
{
    public Action<AiContext> Callback { get; }

    public AiBrainFuncWrap(Action<AiContext> callback)
    {
        Callback = callback;
    }

    public void Execute(AiContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context), "AI context cannot be null.");
        }

        Callback(context);

    }
}
