# Packets and handlers

Browse the [interactive packet reference](/packets/) for every registered packet, its wire
structure, direction, size, handler summary, and source file.

`Moongate.Network.Packets` defines wire formats independently of TCP and the game
server. `PacketRegistry` describes frames and decodes incoming packets;
`IPacketHandler<TPacket>` supplies synchronous game behavior, while
`IAsyncPacketHandler<TPacket>` handles packets that need I/O. These are separate
registrations. The initial built-in formats target **ClassicUO 7.x**.

## Built-in packet coverage

Lengths include the opcode and, for variable packets, the length header.
Directions are relative to the server. This is the default table, not the whole UO protocol:

| Opcode | Class | Direction | Length | Default host handler |
| --- | --- | --- | --- | --- |
| `0x53` | `PopupMessagePacket` | Outgoing | Fixed 2 | Game: sent before disconnecting when character creation is refused or fails |
| `0x55` | `LoginCompletePacket` | Outgoing | Fixed 1 | — |
| `0x73` | `PingPacket` | Both | Fixed 2 | Game: `PingPacketHandler`; Login: `LoginRolePingPacketHandler` |
| `0x80` | `AccountLoginPacket` | Incoming | Fixed 62 | Login: async account check, then `0xA8` list or `0x82` denial |
| `0x82` | `LoginDeniedPacket` | Outgoing | Fixed 2 | — |
| `0x8C` | `ServerRedirectPacket` | Outgoing | Fixed 11 | Login: sent after a valid `0xA0`, before closing the login connection |
| `0x91` | `GameLoginPacket` | Incoming | Fixed 65 | Game: validates and consumes the one-use handoff ticket, then sends `0xB9` and `0xA9` |
| `0xA0` | `ServerSelectPacket` | Incoming | Fixed 3 | Login: checks realm eligibility, issues ticket and redirects |
| `0xA8` | `ServerListPacket` | Outgoing | Variable, minimum 6 | — |
| `0xB9` | `SupportFeaturesPacket` | Outgoing | Fixed 5 | Game: sent after a valid `0x91` |
| `0xBD` | `ClientVersionPacket` | Incoming | Variable, minimum 4 | `ClientVersionPacketHandler`: records the client version on the session |
| `0xBD` | `ClientVersionRequestPacket` | Outgoing | Fixed 3 | — |
| `0xEF` | `LoginSeedPacket` | Incoming | Fixed 21 | `LoginSeedPacketHandler`; on login listeners it also records the client version, which the handoff ticket carries to the game session |

The Ultima plugin adds these packets in game and standalone modes, with
`RegisterIncomingPacket` for the incoming ones (see
[Host integration](#host-integration)):

| Opcode | Class | Direction | Length | Handler |
| --- | --- | --- | --- | --- |
| `0x8D` | `CreateCharacterEnhancedPacket` | Incoming | Variable | `CreateCharacterEnhancedPacketHandler`: creates and saves the character and starting items |
| `0xA9` | `CharacterListPacket` | Outgoing | Variable, minimum 6 | — |
| `0xD9` | `ClientHardwareInfoPacket` | Incoming | Fixed 268 | `IgnoredPacketHandler<T>`: recognised and ignored for now (Debug log) |
| `0xF8` | `CreateCharacterPacket` | Incoming | Fixed 106 | `CreateCharacterPacketHandler`: creates and saves the character and starting items |
| `0x5D` | `PlayCharacterPacket` | Incoming | Fixed 73 | `PlayCharacterPacketHandler`: brings the chosen character into the world |
| `0x83` | `DeleteCharacterPacket` | Incoming | Fixed 39 | `DeleteCharacterPacketHandler`: marks the character for deletion |
| `0x02` | `MoveRequestPacket` | Incoming | Fixed 7 | `MoveRequestPacketHandler`: turns or steps the character, answered with `0x22` or `0x21` |
| `0x22` | `MovementAckPacket` | Outgoing | Fixed 3 | — |
| `0x21` | `MovementRejectPacket` | Outgoing | Fixed 8 | — |
| `0x77` | `MobileMovingPacket` | Outgoing | Fixed 17 | — |
| `0x06` | `UseRequestPacket` | Incoming | Fixed 5 | `UseRequestPacketHandler`: opens a container the character carries, or a paperdoll |
| `0x03` | `AsciiSpeechRequestPacket` | Incoming | Variable, minimum 9 | `SpeechRequestPacketHandler`: local say or in-game dot command |
| `0xAD` | `UnicodeSpeechRequestPacket` | Incoming | Variable, minimum 14 | `SpeechRequestPacketHandler`: Unicode and encoded-keyword say or dot command |
| `0xAE` | `UnicodeSpeechMessagePacket` | Outgoing | Variable, minimum 50 | Player speech and private command output |
| `0x24` | `DisplayContainerPacket` | Outgoing | Fixed 7, or 9 from client 7.0.9.0 | — |
| `0x3C` | `ContainerContentPacket` | Outgoing | Variable, minimum 5 | — |
| `0x09` | `LookRequestPacket` | Incoming | Fixed 5 | `LookRequestPacketHandler`: shows the name over the object (`0xC1`) |
| `0x34`, `0x72` | `MobileQueryPacket`, `WarModeRequestPacket` | Incoming | Fixed 10, 5 | `IgnoredPacketHandler<T>`: recognised and ignored for now (Debug log) |
| `0xC8` | `UpdateRangePacket` | Incoming | Fixed 2 | `UpdateRangePacketHandler`: answers with the server's view range |
| `0xC8` | `ViewRangePacket` | Outgoing | Fixed 2 | — |
| `0x88` | `DisplayPaperdollPacket` | Outgoing | Fixed 66 | — |
| `0x07` | `LiftRequestPacket` | Incoming | Fixed 7 | `LiftRequestPacketHandler`: picks up an item the character carries or wears, or one on the ground within 2 tiles |
| `0x08` | `DropRequestPacket` | Incoming | Fixed 15 | `DropRequestPacketHandler`: drops the held item into a carried container or on the ground |
| `0x25` | `ContainerItemUpdatePacket` | Outgoing | Fixed 21, or 20 before client 6.0.1.7 | — |
| `0x27` | `LiftRejectPacket` | Outgoing | Fixed 2 | — |
| `0x1D` | `RemoveEntityPacket` | Outgoing | Fixed 5 | — |
| `0x1A` | `WorldItemPacket` | Outgoing | Variable, minimum 16 | — |
| `0xF3` | `WorldItemSaPacket` | Outgoing | 24, or 26 from client 7.0.9.0 | — |
| `0x13` | `EquipRequestPacket` | Incoming | Fixed 10 | `EquipRequestPacketHandler`: puts the held item on the character, or bounces it back |
| `0x2E` | `WornItemPacket` | Outgoing | Fixed 15 | — |
| `0x6C` | `TargetCursorPacket` | Outgoing | Fixed 19 | — |
| `0x6C` | `TargetResponsePacket` | Incoming | Fixed 19 | `TargetResponsePacketHandler`: completes the player's pending target |
| `0x05`, `0x22`, `0xB5`, `0xFB` | `AttackRequestPacket`, `ResynchronizeRequestPacket`, `OpenChatWindowPacket`, `PublicHouseContentPacket` | Incoming | Fixed 5, 3, 64, 2 | `IgnoredPacketHandler<T>`: recognised and ignored for now (Debug log) |
| `0x12`, `0xE1` | `TextCommandPacket`, `ClientTypePacket` | Incoming | Variable | `IgnoredPacketHandler<T>`: recognised and ignored for now (Debug log) |
| `0xBF` | `ExtendedCommandPacket` | Incoming | Variable | `ExtendedCommandPacketHandler`: subcommand `0x10` answers a tooltip; the others are ignored for now |
| `0xD6` | `QueryPropertiesPacket` | Incoming | Variable, at most 500 serials | `QueryPropertiesPacketHandler`: one `0xD6` per object the character sees |
| `0xD6` | `PropertyListPacket` | Outgoing | Variable | — |
| `0xDC` | `PropertyListInfoPacket` | Outgoing | Fixed 9 | — |
| `0xC1` | `LocalizedMessagePacket` | Outgoing | Variable | — |
| `0x54` | `PlaySoundPacket` | Outgoing | Fixed 12 | — |

Normal speech (`say`) reaches the speaker and other player characters within 15
tiles on the same map. Whisper, yell, emote, global chat and the separate chat
window are not supported yet. A leading `.` invokes the existing command system
privately; `..` escapes one dot. Empty or over-128-character speech is ignored.

When a character enters the world the server sends, in this order (ModernUO's, checked against
ServUO, UOX3, POL and Source-X): `0x1B` login confirm, `0xBF` subcommand `0x08` map, `0xBC`
season, `0x4F` and `0x4E` light, `0x20` the player, `0x78` the player with worn items, hair and
beard (virtual serials from `IMobileService`), `0x11` status (version 5), `0x72` peace, `0x55`
login complete and `0x5B` time; then `CharacterEnteredWorldEvent` is published. The outgoing
classes live in `Moongate.Server.Ultima.Packets.World`. The character is then a live `MobileEntity`
in `IMobileService`, facing the direction it was saved with.

A step (`0x02`) carries the direction, a running bit (`0x80`) and a sequence. The sequence is 0
after login or a refused step, then 1 to 255 and round again from 1; any other value is refused.
A direction the character does not face only turns it and uses no time. A step in the faced
direction books the next one 400 ms later walking or 200 ms running, and may come up to 200 ms
early; an earlier step, or one `IMovementService` blocks, is refused. `0x22` accepts the step with
the character's notoriety; `0x21` refuses it and puts the client back at the real position.

Players see each other within the view range (18 tiles by default) on both axes. `ISectorService` keeps the live mobiles in
16×16 sectors per map, and `IWorldViewService` recomputes what changed from the old and new
position of each step, as ModernUO and POL do. After the enter-world sequence the player gets
`0x78` of everyone in range and they get its `0x78`. An accepted step or turn sends `0x77` (bit
`0x80` of the direction marks a run) to the players that already saw the mover, `0x78` both ways
to the players that just came into range, and `0x1D` to the players that lost it; the mover's
client drops what it walks away from by itself. When a character leaves the world the players in
range get its `0x1D`. The range is `ultima.world.view_range` (default 18, from 5 to 24); the client's
`0xC8` request is answered with it, whatever the client asked, so both sides use the same range.
`ISectorService.Query` returns the players, the NPCs and the ground items around a point, in the
view range unless another is given. A sector is active while a player stands within two sectors of it (the 5×5
sectors around each player, as ModernUO); NPCs do not wake sectors. `ISectorService.IsActive` tells
it, for the NPC AI to come.

When the session closes, `CharacterLeaveWorldService` (an `ISessionClosedListener`) copies the
character, removes it from the world, saves the copy and publishes `CharacterLeftWorldEvent`. The
world save writes the live characters too, so a crash loses at most the steps since the last save.

The items a character wears, and everything inside them at any depth, are loaded at `0x5D` and
live in `IItemService` while it plays; they leave the world, and are saved after the character,
when its session closes, and the world save writes them too. A double click (`0x06`) on a live
item opens it when its graphic has the tiledata Container flag and the session's character
carries it, worn or inside something worn: `0x24` with the gump from `containers.toml` (the
default entry when the graphic has none), then `0x3C` with its direct contents, even when empty.
Clients before 7.0.9.0 get the 7-byte `0x24`, and before 6.0.1.7 a `0x3C` without the grid byte;
an unknown version gets the modern formats.

A double click on a mobile opens its paperdoll (`0x88`) when its body is human in
`data/bodies.toml` (a monster has none) and it is on the character's map within
`ultima.world.view_range` along X and Y; the character's own paperdoll button sends its serial
with the high bit `0x80000000` set and opens without the range check (the body must still be
human). A body missing from `bodies.toml` has no paperdoll. The title is built as ModernUO's: the fame
and karma prefix of [`titles.toml`](data-files/titles.md), whose rows from 10,000 fame say `Lord`
or `Lady`, the name, then `, <title>` when the mobile has one (NPC templates give titles such as "the
mage"), as "The Glorious Lord Aria, the mage"; players and human NPCs alike. Skill titles are not
added yet. The flags
say war mode (always off for now) and whether the viewer may take items off, set only on the
character's own paperdoll. The worn items are already known to the client from `0x78`. Other
double clicks are not handled yet.

Picking an item up (`0x07`) records it as held in the session; it stays in its container until
the drop. A whole item inside a container the character carries, or one the character wears
(from the paperdoll; not the backpack), can be picked up: holding another item already
(`AreHolding`), part of a non-stackable item or of a worn stack, the backpack or an item not
carried is refused with `0x27` (`CannotLift`); an item of the character's inside a container is shown back with `0x25`, and a
worn one is put back on the paperdoll with `0x2E`, as ModernUO; another player's item is never
shown. A worn item picked up stays on the character until the drop, and the other players in
range see it taken off (`0x1D`). Dropping it (`0x08`)
puts it into a carried container at the drop position, brought inside the gump bounds, or at a
random spot when dropped on the container's icon; dropped on a carried item that is not a
container, it goes into that item's container at that item's position. Mobiles, items not
carried, and a container into itself or anything inside it bounce the item back. Every drop frees
the hand and sends `0x25` with the item's real position, or shows a ground item where it lies.

Lifting part of a stackable item (tiledata `Generic`) splits it: the held part keeps the serial,
since the client drags it, and the rest takes a serial from `IItemSerialPool` (64 serials reserved
from the items sequence, refilled below 16), stays where the stack was and is shown with `0x25`.
With no reserved serial left the lift is refused with `Inspecific`. Dropping the held item onto a
carried stack of the same kind (graphic, hue, template, name and rarity, neither with props), up
to 60000, merges them: the stack grows (`0x25`), the
held item is removed from the client (`0x1D`) and from memory, and its row is deleted by the next
world save in the same transaction as the grown stack, or by the leave save, which takes over the
character's pending deletions and drops them if it fails (the database then still holds both
stacks as before the merge). Dropping onto a container never merges. `0x5D` waits for the leave
saves of the account's last session before it loads the character, so it never reads rows older
than what that session saved. A held item dropped on a paperdoll (`0x13`) is worn when the paperdoll is the character's own and
`IEquipmentService` allows it: the item's own layer (the template's, else tiledata's for a
wearable graphic; the layer the client suggests is ignored, as ModernUO) must be one worn from
the paperdoll (not backpack, hair, beard, mount, shop or bank), free, and fit both hands as
ModernUO and POL: a template with `two_handed_weapon = true` needs both hands free, and nothing
else goes in hand while one is worn; shields and torches go with a one-handed weapon. The item
leaves its container or the ground and everyone in range, the character included, gets `0x2E`.
Anything else bounces back to where the item still is: its container (`0x25`), the ground, or
the character it was taken off (`0x2E` to everyone in range). A stack of more than one is not
worn. The hand is always freed, and a drop or equip that names another item than the held one
puts the held one back. To keep the unique `(mobile_id, layer)` index satisfied after a change of
clothes, the world save and the leave save delete removed rows first and write unworn items
before worn ones, and a leave save skips an item the character dropped that someone else now
carries or wears (that owner's save writes it).

Items on the ground live in the sector grid with the mobiles. Dropping the held item on the
ground (`0x08` with destination `0xFFFFFFFF`) works within 2 tiles of the character, in line of
sight from its eyes: the client's Z is ignored and the item lands on the highest surface of the
land or a static up to 16 above the character's feet, as ModernUO; other ground items are not
stacked on, so two items on one tile overlap. It is shown to everyone in range, the dropper
included: `0x1A` for clients before 7.0.0.0, `0xF3` after, two bytes longer from 7.0.9.0.
Anyone can pick up (`0x07`) a ground item within 2 tiles in line of sight; it leaves every screen
(`0x1D`) while held, and a partial lift leaves the rest on the ground with a new serial. Too far
or out of sight is refused with `OutOfRange` or `OutOfSight`, and the item is shown again to that
player only; an item someone else holds is never shown. A held
ground item that bounces goes back where it lay, and one still held when the session closes is
put back too. Dropping onto a ground stack within reach merges them as in the backpack. Players
walking into range of a ground item, or entering the world near it, get it with the same old and
new position test as the mobiles. The items on the ground and everything inside them are loaded
at startup and saved by the world save. A ground item decays after its template's time, 60 minutes
unless `decay_minutes` says otherwise, counted from when it landed on the ground and restarted
each time it is put down again; a decayed container takes its contents with it (see
[Templates](templates.md)). A container on the ground cannot be opened yet.

`ITargetService` shows a player the target cursor (`0x6C`) and hands the pick to a callback on the
game loop, or to a command awaiting `RequestAsync`. A player has one target at a time: a new one
ends the old as overridden, `Cancel` sends `0x6C` with flags 3 and id 0, and a closing session ends
it as disconnected. Cursor ids count up per session, and a response with another id is ignored.
The response is resolved as ModernUO does: x and y -1 with no serial is the player's cancel; a
serial must be a live item or mobile; the ground takes the map's average height, not the client's;
a static must really be there and gives its top (half the height of a bridge). Range and line of
sight are left to the caller. In-game commands run detached from the speech packet, so a command
waiting for a target does not hold back the session's packets; its output arrives when it ends.
A player runs one command at a time: another one meanwhile is refused with "A command is already
running."
`.where` (game masters) prints what a target picks.

The same opcode can have different definitions in each direction, as with `0xBD`.
The realm list is filtered by the authenticated account's minimum realm level.
It contains each realm's IPv4 address but no port. `0xA0` selects a live eligible
realm and `0x8C` supplies its IPv4 address, port and one-use key. The login sender
flushes `0x8C` before closing the login connection. On the new game connection
the client sends that key as a raw four-byte seed, followed by `0x91` with the
same key, username and password. The game checks the seed and atomically consumes
the Redis ticket. The ticket expires 30 seconds after it is issued, so the game
reconnect must complete within that time. A direct `0xEF` client-version seed
still works on game listeners.

After a valid `0x91` the game server copies the client version from the ticket to
the session, turns on Huffman compression for everything it sends from then on,
and sends `0xB9` followed by `0xA9`: the account's saved characters in the
configured number of slots (`ultima.characters.max_per_account`, default 7), plus the
starting cities from `data/starting_cities.toml`. The creation handlers save a new
character and its starting items in one transaction. `0x5D` brings the chosen
character into the world (see below).

`TryGetDescriptor(opCode, out descriptor)` prefers incoming, then outgoing;
`descriptor.PacketType.Name` gives its class name. The overload accepting
`PacketDirection` selects one direction explicitly when needed.

`registry.TryDecode(bytes, out packet, out opCode)` always sets `opCode` to the
first byte, even on failure. Empty input returns false with opcode zero; inspect
input length to distinguish that case. Decoding needs one **complete incoming
frame**, including its header. It does not buffer a TCP stream. Outgoing-only
packets have metadata but no incoming parser.

## Tooltips

The character list flags (`0xA9`) include AOS (`0x20`), so the client uses AOS tooltips
(object property lists) and asks for them with `0xD6` (a list of serials, at most 500; a
length that is not whole serials, or more serials, cannot be read and closes the connection, as
any unreadable packet) or `0xBF` subcommand `0x10` (one serial). The
server answers one `0xD6` per object the character can see: an item it carries or wears, an
item worn by a mobile or lying on the ground within `ultima.world.view_range`, or a mobile in
that range on its map; anything else gets nothing. `ITooltipService` builds the lines, as
ModernUO and UOX3:

- an item's name: the client's cliloc for its graphic (1020000 + graphic, 1078872 + graphic
  from `0x4000`), which the client shows in its own language, or the item's or template's
  name as text; a stack uses 1050039 with the amount;
- in the server language, from the message files (`ILocalizationService`, as UOX3):
  blessed or newbied (9055 "[Blessed]") or cursed (30005), the item's loot type else the
  template's; the weight of the whole stack (30006 / 30007); the rarity (30000–30004),
  coloured (common white). A text missing from the files falls back to English;
- a mobile: 1050045 with its name and title.

Names (items' clilocs, amounts, mobiles' names and titles) stay the client's until the server
has translated names; every other line is the server's, so the tooltips follow the server
language.

Free text goes through the clilocs whose whole text is `~1_NOTHING~` (1042971, 1070722, ...),
one per line; an argument is cut at 504 characters, which older clients cannot exceed. The
tooltip's revision is a 26-bit hash of its lines, as ModernUO: `0xD6` carries it, and `0xDC`
carries it with bit 30 set. As ModernUO, what is shown is followed by its `0xDC`: a
mobile coming into view (`0x78`) with each of its worn items, a ground item (`0x1A`/`0xF3`), an
item put on (`0x2E`), each item of an opened container (`0x3C`), and a container item updated
(`0x25`) after a split, a merge, a placement or a bounce; the client asks again when a revision
changes. The character's own `0x78` at world entry is not followed yet: the client asks for
tooltips it does not have when the cursor is over them. A tooltip depends only on a few fields of its
object (for an item: template, graphic, amount, name, rarity, loot type, movable; for a mobile:
name and title), so it is cached by them: equal objects share one tooltip, and a change gives
another key, so nothing is ever invalidated. The cache keeps up to 10000 tooltips and starts
over when full. A single click (`0x09`) shows the first line, the name, over the
object with `0xC1`.

## Define a packet and test its bytes

This example uses a private illustrative opcode. It is not an addition to the
ClassicUO protocol; use it only with a matching test client. Reference
`Moongate.Network.Packets` and place this in `ExamplePacket.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

[PacketHandler(0xFE, PacketSizing.Fixed, Length = 3)]
public sealed class ExamplePacket : BaseFixedPacket<ExamplePacket>,
    IIncomingPacket<ExamplePacket>, IOutgoingPacket
{
    public ushort Value { get; }

    public ExamplePacket(ushort value)
    {
        Value = value;
    }

    public static bool TryParse(ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out ExamplePacket? packet)
    {
        packet = null;
        if (!HasValidHeader(data))
        {
            return false;
        }
        var reader = new PacketReader(data[1..]);
        if (!reader.TryReadUInt16BigEndian(out var value))
        {
            return false;
        }
        packet = new ExamplePacket(value);
        return true;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(Value);
    }
}
```

The metadata attribute is named `PacketHandler`, but describes the **wire packet**;
it does not register the game handler class. Call `RegisterIncoming<T>()` for a
packet that implements `IIncomingPacket<T>` (a bidirectional packet covers both
directions in one call), or `RegisterOutgoing<T>()` for one that implements only
`IOutgoingPacket`. Duplicate types or conflicting opcode/direction pairs fail
registration. Freeze only after composing the table.

Run this `Program.cs` as a byte-level smoke test without a server or client:

```csharp
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Serialization;

var registry = new PacketRegistry();
PacketTable.Register(registry); // Optional: include the built-in formats.
registry.RegisterIncoming<ExamplePacket>();
registry.Freeze();

var bytes = PacketCodec.Encode(new ExamplePacket(0x1234));
if (!bytes.AsSpan().SequenceEqual(new byte[] { 0xFE, 0x12, 0x34 }) ||
    !registry.TryDecode(bytes, out var packet, out var opCode) ||
    packet is not ExamplePacket { Value: 0x1234 } || opCode != 0xFE)
{
    throw new InvalidOperationException("Packet round trip failed");
}
if (registry.TryDecode(new byte[] { 0xFE }, out _, out var failedOpCode) || failedOpCode != 0xFE)
{
    throw new InvalidOperationException("Malformed-frame opcode was lost");
}
Console.WriteLine("Packet bytes and failure opcode verified");
```

For variable packets, use `BasePacket<T>` with
`[PacketHandler(opcode, PacketSizing.Variable, MinimumLength = ...)]`, validate the
whole declared frame and write its complete length header. See the built-in
`ClientVersionPacket` and `ServerListPacket` for examples of parsing and writing.

## Register a game handler

Add a reference to `Moongate.Server.Core`. `ExamplePacketHandler.cs` can echo the
packet through the bounded sender without waiting for socket I/O:

```csharp
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;

public sealed class ExamplePacketHandler : IPacketHandler<ExamplePacket>
{
    private readonly IPacketSendService _sender;

    public ExamplePacketHandler(IPacketSendService sender)
    {
        _sender = sender;
    }

    public void Handle(GameSession session, ExamplePacket packet)
    {
        if (!_sender.TrySend(session.SessionId, new ExamplePacket(packet.Value)))
        {
            // The sender owns and observes connection cleanup.
            _ = _sender.DisconnectAsync(session.SessionId);
        }
    }
}
```

In `Program.cs` service composition, or a plugin's `Register` callback, import
`Moongate.Server.Core.Extensions` and call:

```csharp
container.RegisterPacketHandler<ExamplePacket, ExamplePacketHandler>();
```

Handlers are singletons. Keep per-player state in the session/world, not mutable
handler fields. `Handle` executes on the game loop; keep it short and synchronous.
`TrySend` snapshots encoded bytes and returns admission status, not delivery
confirmation. Decide what to do when it returns false; the example disconnects.

For a handler that awaits database or network I/O, implement
`IAsyncPacketHandler<TPacket>` and register it with
`RegisterAsyncPacketHandler<TPacket, THandler>()`. Its `HandleAsync` runs off
the game loop. The handler receives a `PacketContext` rather than a mutable
`GameSession`. This example assumes an application-specific lookup service:

```csharp
// Define this service in your plugin; keep its interface in a separate file.
public interface IExampleLookup
{
    Task<ushort?> LoadAsync(ushort value, CancellationToken cancellationToken);
}

public sealed class ExampleAsyncPacketHandler : IAsyncPacketHandler<ExamplePacket>
{
    private readonly IExampleLookup _lookup;

    public ExampleAsyncPacketHandler(IExampleLookup lookup)
    {
        _lookup = lookup;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        ExamplePacket packet,
        CancellationToken cancellationToken
    )
    {
        var value = await _lookup.LoadAsync(packet.Value, cancellationToken);

        if (value is not null)
        {
            context.TrySend(new ExamplePacket(value.Value));
        }
    }
}

container.RegisterAsyncPacketHandler<ExamplePacket, ExampleAsyncPacketHandler>();
```

When the result must change game state, return to the loop with
`await context.RunOnGameLoopAsync(session => { /* update session/world */ }, cancellationToken)`.
It returns `false` if the original session disconnected before the action ran.
An async handler must not mutate a session directly after an `await`.

Only one async packet may be in flight for a session. Packets the session sends
meanwhile wait, up to 32 (`PacketDispatchService.MaxPendingPerSession`), and are
dispatched in arrival order when it finishes; one more is rejected and the client
disconnected. The executor accepts at most 64 operations at once and runs at most
four handlers concurrently.
Admission stays nonblocking. Disconnect and server shutdown cancel the
handler token; observe it in every awaited I/O call. Exceptions are logged
without packet payloads and do not stop the game loop.

### Host integration

**A new incoming packet needs two registrations.** The handler registration does
not add the opcode to the wire table. Add the packet type too:

```csharp
container.RegisterIncomingPacket<ExamplePacket>();
container.RegisterPacketHandler<ExamplePacket, ExamplePacketHandler>();
```

At startup the host builds one registry from `PacketTable` plus every
`RegisterIncomingPacket` call, freezes it, and gives it to the framers, the login
and game listeners and the dispatcher. Outgoing packets need no registration to be
sent. A packet registered without a handler is decoded but rejected, and the game
server closes the connection, as it does for an unknown opcode. Register
`IgnoredPacketHandler<T>` for a packet the client sends that the server does not
act on yet.

The host also registers LoginSeed and an async AccountLogin handler. The latter
checks credentials against `IAccountService`, sends `0x82` for denied login or
an empty eligible realm list, and sends a filtered `0xA8` list after success.
The login-only host uses a dedicated ordered async connection pipeline; standalone
runs separate login and game listeners. Selection and handoff use Redis-backed
leases and one-use tickets ([Implementation status](implementation-status.md)). See
[Transport and game ownership](network-game-separation.md) for connection lifecycle,
queue limits and overload policy, and [Game loop and timers](game-loop-and-timers.md)
for thread ownership and completion.
