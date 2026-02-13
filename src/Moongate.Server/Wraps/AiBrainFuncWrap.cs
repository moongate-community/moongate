using Moongate.UO.Data.Contexts;
using Moongate.UO.Data.Interfaces.Ai;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Wraps;

public class AiBrainFuncWrap : IAiBrainAction
{
    public Action<AiContext> Callback { get; }

    public Action<AiContext, string, UOMobileEntity> ReceiveSpeechCallback { get; }

    public AiBrainFuncWrap(Action<AiContext> callback, Action<AiContext, string, UOMobileEntity> receiveSpeechCallback)
    {
        if (callback == null)
        {
            throw new ArgumentNullException(nameof(callback), "Callback cannot be null.");
        }

        if (receiveSpeechCallback == null)
        {
            throw new ArgumentNullException(nameof(receiveSpeechCallback), "ReceiveSpeechCallback cannot be null.");
        }

        Callback = callback;
        ReceiveSpeechCallback = receiveSpeechCallback;
    }

    public void Execute(AiContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context), "AI context cannot be null.");
        }

        Callback(context);
    }

    public void ReceiveSpeech(AiContext context, string speech, UOMobileEntity from)
    {
        ReceiveSpeechCallback(context, speech, from);
    }
}
