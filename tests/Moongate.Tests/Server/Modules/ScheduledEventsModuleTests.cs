using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Data.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class ScheduledEventsModuleTests
{
    [Test]
    public void Register_WhenDefinitionIsValid_ShouldStoreDefinition()
    {
        var definitions = new ScheduledEventDefinitionService();
        var module = new ScheduledEventsModule(definitions);
        var script = new Script();
        var definition = script.DoString(
                             """
                             return {
                                 trigger_name = "town_crier_announcement",
                                 recurrence = "daily",
                                 time = "09:00",
                                 payload = {
                                     message = "Hear ye!"
                                 }
                             }
                             """
                         )
                         .Table;

        var registered = module.Register("town_crier_morning", definition);
        var resolved = definitions.TryGet("town_crier_morning", out var scheduledEvent);

        Assert.Multiple(
            () =>
            {
                Assert.That(registered, Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(scheduledEvent, Is.Not.Null);
                Assert.That(scheduledEvent!.TriggerName, Is.EqualTo("town_crier_announcement"));
                Assert.That(scheduledEvent.RecurrenceType, Is.EqualTo(ScheduledRecurrenceType.Daily));
            }
        );
    }
}
