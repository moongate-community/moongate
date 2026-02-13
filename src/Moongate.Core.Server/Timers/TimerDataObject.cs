namespace Moongate.Core.Server.Timers;

public class TimerDataObject : IDisposable
{
    private readonly object _lock = new();
    public string Name { get; set; }

    public string Id { get; set; }

    public double IntervalInMs { get; set; }

    public Action Callback { get; set; }
    public bool Repeat { get; set; }

    public double RemainingTimeInMs;
    public double DelayInMs { get; set; }

    public bool DieOnException { get; set; } = true;

    public void DecrementRemainingTime(double deltaTime)
    {
        if (Monitor.TryEnter(_lock))
        {
            try
            {
                if (DelayInMs > 0)
                {
                    DelayInMs -= deltaTime;

                    if (DelayInMs > 0)
                    {
                        return;
                    }
                }

                RemainingTimeInMs -= deltaTime;
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }
    }

    public void Dispose()
    {
        Callback = null;
        Name = null;
        Id = null;
        IntervalInMs = 0;
        RemainingTimeInMs = 0;
        Repeat = false;
        DelayInMs = 0;

        GC.SuppressFinalize(this);
    }

    public void ResetRemainingTime()
    {
        if (Monitor.TryEnter(_lock))
        {
            try
            {
                RemainingTimeInMs = IntervalInMs;
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }
    }

    public override string ToString()
        => $"Timer: {Name}, Id: {Id}, Interval: {IntervalInMs}, RemainingTime: {RemainingTimeInMs}, Repeat: {Repeat}";
}
