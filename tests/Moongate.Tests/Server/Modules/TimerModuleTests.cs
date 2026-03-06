using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Modules;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class TimerModuleTests
{
    private sealed class TimerModuleTestTimerService : ITimerService
    {
        public string? LastName { get; private set; }

        public TimeSpan LastInterval { get; private set; }

        public TimeSpan? LastDelay { get; private set; }

        public bool LastRepeat { get; private set; }

        public Action? LastCallback { get; private set; }

        public string NextTimerId { get; set; } = "timer-1";

        public string? LastUnregisterTimerId { get; private set; }

        public string? LastUnregisterByName { get; private set; }

        public int UnregisterByNameResult { get; set; }

        public int ProcessedTicksCount { get; private set; }

        public bool UnregisterTimerResult { get; set; } = true;

        public bool UnregisterAllCalled { get; private set; }

        public void ProcessTick()
            => ProcessedTicksCount++;

        public string RegisterTimer(
            string name,
            TimeSpan interval,
            Action callback,
            TimeSpan? delay = null,
            bool repeat = false
        )
        {
            LastName = name;
            LastInterval = interval;
            LastDelay = delay;
            LastRepeat = repeat;
            LastCallback = callback;

            return NextTimerId;
        }

        public void UnregisterAllTimers()
            => UnregisterAllCalled = true;

        public bool UnregisterTimer(string timerId)
        {
            LastUnregisterTimerId = timerId;

            return UnregisterTimerResult;
        }

        public int UnregisterTimersByName(string name)
        {
            LastUnregisterByName = name;

            return UnregisterByNameResult;
        }

        public int UpdateTicksDelta(long timestampMilliseconds)
        {
            _ = timestampMilliseconds;

            return 0;
        }
    }

    [Test]
    public void After_ShouldRegisterOneShotTimerAndReturnTimerId()
    {
        var timerService = new TimerModuleTestTimerService
        {
            NextTimerId = "timer-after"
        };
        var module = new TimerModule(timerService);
        var script = new Script();
        var callback = script.DoString("return function() end").Function;

        var timerId = module.After("test_after", 250, callback);

        Assert.Multiple(
            () =>
            {
                Assert.That(timerId, Is.EqualTo("timer-after"));
                Assert.That(timerService.LastName, Is.EqualTo("test_after"));
                Assert.That(timerService.LastInterval, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
                Assert.That(timerService.LastDelay, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
                Assert.That(timerService.LastRepeat, Is.False);
                Assert.That(timerService.LastCallback, Is.Not.Null);
            }
        );
    }

    [Test]
    public void Callback_WhenInvoked_ShouldExecuteLuaClosure()
    {
        var timerService = new TimerModuleTestTimerService();
        var module = new TimerModule(timerService);
        var script = new Script();
        script.DoString("counter = 0");
        var callback = script.DoString("return function() counter = counter + 1 end").Function;

        _ = module.After("test_callback", 10, callback);
        timerService.LastCallback!.Invoke();

        Assert.That(script.Globals.Get("counter").Number, Is.EqualTo(1));
    }

    [Test]
    public void Cancel_ShouldForwardToTimerService()
    {
        var timerService = new TimerModuleTestTimerService
        {
            UnregisterTimerResult = true
        };
        var module = new TimerModule(timerService);

        var canceled = module.Cancel("timer-abc");

        Assert.Multiple(
            () =>
            {
                Assert.That(canceled, Is.True);
                Assert.That(timerService.LastUnregisterTimerId, Is.EqualTo("timer-abc"));
            }
        );
    }

    [Test]
    public void CancelAll_ShouldForwardToTimerService()
    {
        var timerService = new TimerModuleTestTimerService();
        var module = new TimerModule(timerService);

        module.CancelAll();

        Assert.That(timerService.UnregisterAllCalled, Is.True);
    }

    [Test]
    public void CancelByName_ShouldForwardToTimerService()
    {
        var timerService = new TimerModuleTestTimerService
        {
            UnregisterByNameResult = 3
        };
        var module = new TimerModule(timerService);

        var removed = module.CancelByName("npc_orion");

        Assert.Multiple(
            () =>
            {
                Assert.That(removed, Is.EqualTo(3));
                Assert.That(timerService.LastUnregisterByName, Is.EqualTo("npc_orion"));
            }
        );
    }

    [Test]
    public void Every_ShouldRegisterRepeatingTimerAndReturnTimerId()
    {
        var timerService = new TimerModuleTestTimerService
        {
            NextTimerId = "timer-every"
        };
        var module = new TimerModule(timerService);
        var script = new Script();
        var callback = script.DoString("return function() end").Function;

        var timerId = module.Every("test_every", 1000, callback, 500);

        Assert.Multiple(
            () =>
            {
                Assert.That(timerId, Is.EqualTo("timer-every"));
                Assert.That(timerService.LastName, Is.EqualTo("test_every"));
                Assert.That(timerService.LastInterval, Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
                Assert.That(timerService.LastDelay, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
                Assert.That(timerService.LastRepeat, Is.True);
            }
        );
    }
}
