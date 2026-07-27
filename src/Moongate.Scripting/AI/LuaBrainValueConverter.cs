using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Types;
using MoonSharp.Interpreter;

namespace Moongate.Scripting.AI;

internal sealed class LuaBrainValueConverter
{
    private readonly Script _script;

    public LuaBrainValueConverter(Script script)
    {
        _script = script;
    }

    public Table ToContext(BrainContext context)
    {
        var table = new Table(_script);
        table.Set("now_ms", DynValue.NewNumber(context.Now.ToUnixTimeMilliseconds()));
        table.Set("self", DynValue.NewTable(ToMobileSnapshot(context.Self)));

        var home = new Table(_script);
        home.Set("map_id", DynValue.NewNumber(context.HomeMapId));
        home.Set("position", DynValue.NewTable(ToPosition(context.HomePosition)));
        table.Set("home", DynValue.NewTable(home));

        var nearby = new Table(_script);

        foreach (var mobile in context.Nearby)
        {
            nearby.Append(DynValue.NewTable(ToMobileSnapshot(mobile)));
        }

        table.Set("nearby", DynValue.NewTable(nearby));

        return table;
    }

    public Table? ToEvent(NpcBrainEvent? brainEvent)
    {
        if (brainEvent is null)
        {
            return null;
        }

        var table = new Table(_script);
        table.Set("type", DynValue.NewString(brainEvent.Type.ToString().ToLowerInvariant()));

        switch (brainEvent.Type)
        {
            case NpcBrainEventType.Activate:
            case NpcBrainEventType.Deactivate:
                break;
            case NpcBrainEventType.SpeechHeard:
                SetSpeechFields(table, brainEvent);
                break;
            case NpcBrainEventType.MobileEnteredRange:
            case NpcBrainEventType.MobileLeftRange:
                SetMobileFields(table, brainEvent);
                SetPosition(table, "from_position", brainEvent.FromPosition);
                SetPosition(table, "to_position", brainEvent.ToPosition);
                break;
            case NpcBrainEventType.MobileMoved:
                SetMobileFields(table, brainEvent);
                SetPosition(table, "from_position", brainEvent.FromPosition);
                SetPosition(table, "to_position", brainEvent.ToPosition);
                break;
            case NpcBrainEventType.Attacked:
                SetSubjectSerial(table, "attacker_id", brainEvent.Mobile);
                break;
            case NpcBrainEventType.Damage:
                SetSubjectSerial(table, "attacker_id", brainEvent.Mobile);
                table.Set("amount", DynValue.NewNumber(brainEvent.Amount));
                break;
            case NpcBrainEventType.Death:
                SetSubjectSerial(table, "killer_id", brainEvent.Mobile);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(brainEvent), brainEvent.Type, null);
        }

        return table;
    }

    private Table ToMobileSnapshot(BrainMobileSnapshot mobile)
    {
        var table = new Table(_script);
        table.Set("id", DynValue.NewNumber(mobile.Id.Value));
        table.Set("name", DynValue.NewString(mobile.Name));
        table.Set("is_player", DynValue.NewBoolean(mobile.IsPlayer));
        table.Set("map_id", DynValue.NewNumber(mobile.MapId));
        table.Set("position", DynValue.NewTable(ToPosition(mobile.Position)));
        table.Set("hits", DynValue.NewNumber(mobile.Hits));
        table.Set("hits_max", DynValue.NewNumber(mobile.HitsMax));
        table.Set(
            "hits_percent",
            DynValue.NewNumber(mobile.HitsMax <= 0 ? 0 : mobile.Hits * 100.0 / mobile.HitsMax)
        );
        table.Set("warmode", DynValue.NewBoolean(mobile.Warmode));
        SetOptionalSerial(table, "combatant_id", mobile.CombatantId);
        table.Set("criminal", DynValue.NewBoolean(mobile.Criminal));
        table.Set("kills", DynValue.NewNumber(mobile.Kills));

        return table;
    }

    private Table ToPosition(Point3D position)
    {
        var table = new Table(_script);
        table.Set("x", DynValue.NewNumber(position.X));
        table.Set("y", DynValue.NewNumber(position.Y));
        table.Set("z", DynValue.NewNumber(position.Z));

        return table;
    }

    private void SetMobileFields(Table table, NpcBrainEvent brainEvent)
    {
        var mobile = brainEvent.Mobile;
        SetSubjectSerial(table, "mobile_id", mobile);

        if (mobile is null)
        {
            table.Set("mobile_name", DynValue.Nil);
            table.Set("mobile_is_player", DynValue.Nil);
            return;
        }

        table.Set("mobile_name", DynValue.NewString(mobile.Name));
        table.Set("mobile_is_player", DynValue.NewBoolean(mobile.IsPlayer));
    }

    private void SetPosition(Table table, string name, Point3D? position)
        => table.Set(name, position.HasValue ? DynValue.NewTable(ToPosition(position.Value)) : DynValue.Nil);

    private void SetOptionalSerial(Table table, string name, Serial serial)
        => table.Set(name, serial == Serial.Zero ? DynValue.Nil : DynValue.NewNumber(serial.Value));

    private void SetSpeechFields(Table table, NpcBrainEvent brainEvent)
    {
        var speaker = brainEvent.Mobile;
        SetSubjectSerial(table, "speaker_id", speaker);

        if (speaker is null)
        {
            table.Set("speaker_name", DynValue.Nil);
            table.Set("speaker_is_player", DynValue.Nil);
        }
        else
        {
            table.Set("speaker_name", DynValue.NewString(speaker.Name));
            table.Set("speaker_is_player", DynValue.NewBoolean(speaker.IsPlayer));
        }

        table.Set(
            "speech_type",
            brainEvent.SpeechType.HasValue
                ? DynValue.NewString(brainEvent.SpeechType.Value.ToString().ToLowerInvariant())
                : DynValue.Nil
        );
        table.Set("text", brainEvent.Text is null ? DynValue.Nil : DynValue.NewString(brainEvent.Text));
    }

    private static void SetSubjectSerial(Table table, string name, BrainMobileSnapshot? mobile)
        => table.Set(name, mobile is null ? DynValue.Nil : DynValue.NewNumber(mobile.Id.Value));
}
