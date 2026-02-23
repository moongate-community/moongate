using BenchmarkDotNet.Attributes;
using Moongate.Server.Services.Timing;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class TimerWheelBenchmark
{
    private TimerWheelService _timerWheelService = null!;
    private long _timestampMilliseconds;

    [GlobalSetup]
    public void Setup()
    {
        _timerWheelService = new(
            new()
            {
                TickDuration = TimeSpan.FromMilliseconds(8),
                WheelSize = 512
            }
        );

        for (var i = 0; i < 512; i++)
        {
            _timerWheelService.RegisterTimer(
                $"bench_timer_{i}",
                TimeSpan.FromMilliseconds(64),
                static () => { },
                repeat: true
            );
        }

        _timestampMilliseconds = 1_000_000;
        _timerWheelService.UpdateTicksDelta(_timestampMilliseconds);
    }

    [Benchmark]
    public int UpdateTicksDelta()
    {
        _timestampMilliseconds += 8;

        return _timerWheelService.UpdateTicksDelta(_timestampMilliseconds);
    }
}
