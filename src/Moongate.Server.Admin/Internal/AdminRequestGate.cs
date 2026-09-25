using Grpc.Core;

namespace Moongate.Server.Admin.Internal;

internal sealed class AdminRequestGate
{
    private readonly Lock _sync = new();
    private readonly int _capacity;
    private bool _accepting;
    private int _active;

    public AdminRequestGate(int capacity)
    {
        _capacity = capacity;
    }

    public void Activate()
    {
        lock (_sync)
        {
            _accepting = true;
        }
    }

    public void StopAccepting()
    {
        lock (_sync)
        {
            _accepting = false;
        }
    }

    public StatusCode TryEnter()
    {
        lock (_sync)
        {
            if (!_accepting)
            {
                return StatusCode.Unavailable;
            }

            if (_active == _capacity)
            {
                return StatusCode.ResourceExhausted;
            }

            _active++;

            return StatusCode.OK;
        }
    }

    public void Exit()
    {
        lock (_sync)
        {
            _active--;
        }
    }
}
