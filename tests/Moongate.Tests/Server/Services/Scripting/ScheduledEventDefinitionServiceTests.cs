using Moongate.Server.Data.Scripting;
using Moongate.Server.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class ScheduledEventDefinitionServiceTests
{
    [Test]
    public void Register_WhenDefinitionIsValid_ShouldStoreScheduledEvent()
    {
        var service = new ScheduledEventDefinitionService();
        var script = new Script();
        var definition = BuildWeeklyDefinition(script);

        var registered = service.Register("town_crier_morning", definition, "scripts/events/town_crier.lua");
        var resolved = service.TryGet("town_crier_morning", out var scheduledEvent);

        Assert.Multiple(
            () =>
            {
                Assert.That(registered, Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(scheduledEvent, Is.Not.Null);
                Assert.That(scheduledEvent!.EventId, Is.EqualTo("town_crier_morning"));
                Assert.That(scheduledEvent.TriggerName, Is.EqualTo("town_crier_announcement"));
                Assert.That(scheduledEvent.RecurrenceType, Is.EqualTo(ScheduledRecurrenceType.Weekly));
                Assert.That(scheduledEvent.Time, Is.EqualTo("09:00"));
                Assert.That(scheduledEvent.TimeZone, Is.EqualTo("Europe/Rome"));
                Assert.That(scheduledEvent.DaysOfWeek, Is.EqualTo(new[] { "monday", "wednesday" }));
                Assert.That(scheduledEvent.Payload, Is.Not.Null);
                Assert.That(scheduledEvent.ScriptPath, Is.EqualTo("scripts/events/town_crier.lua"));
            }
        );
    }

    [Test]
    public void Register_WhenEnabledIsMissing_ShouldDefaultToTrue()
    {
        var service = new ScheduledEventDefinitionService();
        var script = new Script();
        var definition = BuildWeeklyDefinition(script);
        definition["enabled"] = DynValue.Nil;

        _ = service.Register("town_crier_morning", definition);
        _ = service.TryGet("town_crier_morning", out var scheduledEvent);

        Assert.That(scheduledEvent!.Enabled, Is.True);
    }

    [Test]
    public void Register_WhenOnceEventHasNoStartAt_ShouldThrow()
    {
        var service = new ScheduledEventDefinitionService();
        var script = new Script();
        var definition = BuildWeeklyDefinition(script);
        definition["recurrence"] = "once";
        definition["start_at"] = DynValue.Nil;
        definition["time"] = DynValue.Nil;
        definition["days_of_week"] = DynValue.Nil;

        Assert.That(
            () => service.Register("one_shot", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("requires 'start_at'")
        );
    }

    [Test]
    public void Register_WhenTriggerNameIsMissing_ShouldThrow()
    {
        var service = new ScheduledEventDefinitionService();
        var script = new Script();
        var definition = BuildWeeklyDefinition(script);
        definition["trigger_name"] = DynValue.Nil;

        Assert.That(
            () => service.Register("town_crier_morning", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("missing 'trigger_name'")
        );
    }

    [Test]
    public void Register_WhenWeeklyEventHasNoDaysOfWeek_ShouldThrow()
    {
        var service = new ScheduledEventDefinitionService();
        var script = new Script();
        var definition = BuildWeeklyDefinition(script);
        definition["days_of_week"] = DynValue.Nil;

        Assert.That(
            () => service.Register("town_crier_morning", definition),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("requires 'days_of_week'")
        );
    }

    private static Table BuildWeeklyDefinition(Script script)
        => new(script)
        {
            ["enabled"] = true,
            ["trigger_name"] = "town_crier_announcement",
            ["recurrence"] = "weekly",
            ["time"] = "09:00",
            ["time_zone"] = "Europe/Rome",
            ["days_of_week"] = new Table(script)
            {
                [1] = "monday",
                [2] = "wednesday"
            },
            ["payload"] = new Table(script)
            {
                ["message"] = "Hear ye!"
            }
        };
}
