using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Modules;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class AsyncJobModuleTests
{
    private sealed class TestAsyncLuaJobService : IAsyncLuaJobService
    {
        public string? LastJobName { get; private set; }
        public string? LastKey { get; private set; }
        public string? LastRequestId { get; private set; }
        public Table? LastPayload { get; private set; }

        public bool RunResult { get; set; } = true;
        public bool TryRunResult { get; set; } = true;

        public bool Run(string jobName, string requestId, Table? payload = null)
        {
            LastJobName = jobName;
            LastRequestId = requestId;
            LastPayload = payload;
            LastKey = null;
            return RunResult;
        }

        public bool TryRun(string jobName, string key, string requestId, Table? payload = null)
        {
            LastJobName = jobName;
            LastKey = key;
            LastRequestId = requestId;
            LastPayload = payload;
            return TryRunResult;
        }
    }

    [Test]
    public void Run_ShouldDelegateToAsyncLuaJobService()
    {
        var service = new TestAsyncLuaJobService();
        var module = new AsyncJobModule(service);
        var payload = new Table(new Script()) { ["text"] = "hello" };

        var ok = module.Run("echo", "req-1", payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(service.LastJobName, Is.EqualTo("echo"));
                Assert.That(service.LastRequestId, Is.EqualTo("req-1"));
                Assert.That(service.LastPayload, Is.SameAs(payload));
            }
        );
    }

    [Test]
    public void TryRun_ShouldDelegateToAsyncLuaJobService()
    {
        var service = new TestAsyncLuaJobService();
        var module = new AsyncJobModule(service);
        var payload = new Table(new Script()) { ["value"] = 7 };

        var ok = module.TryRun("scan", "npc:1", "req-2", payload);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(service.LastJobName, Is.EqualTo("scan"));
                Assert.That(service.LastKey, Is.EqualTo("npc:1"));
                Assert.That(service.LastRequestId, Is.EqualTo("req-2"));
                Assert.That(service.LastPayload, Is.SameAs(payload));
            }
        );
    }
}
