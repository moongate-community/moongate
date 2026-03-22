# Packet System

Moongate v2 uses concrete packet classes implementing `IGameNetworkPacket`.

## Packet Contract

```csharp
public interface IGameNetworkPacket
{
    byte OpCode { get; }
    int Length { get; } // -1 for variable-length
    bool TryParse(ReadOnlySpan<byte> data);
    void Write(ref SpanWriter writer);
}
```

## Registration Model

Packets are decorated with:

```csharp
[PacketHandler(0x02, PacketSizing.Fixed, Length = 7, Description = "Move Request")]
```

Key types:

- `PacketHandlerAttribute`
  - `OpCode`, `Sizing`, `Length`, `Description`
- `PacketSizing`
  - `Fixed`, `Variable`
- `PacketRegistry`
  - metadata + packet factory per opcode
- `PacketTable.Register(PacketRegistry)`
  - generated registration entrypoint

## Runtime Use

`NetworkService` uses `PacketRegistry` to:

1. resolve descriptor (`TryGetDescriptor`)
2. create packet instance (`TryCreatePacket`)
3. call `TryParse(rawPacket)`
4. publish `IncomingGamePacket` to message bus

`PacketDispatchService` then routes by opcode to registered `IPacketListener` instances.

Before packet dispatch, `NetworkService` also handles the login bootstrap boundary:

- `0xEF` login seed remains plain and sets the session seed/client version
- plain `0x80` and `0x91` bootstraps continue to work
- when `game.encryptionMode` allows it, Moongate can autodetect encrypted `0x80` and `0x91` bootstrap packets and attach transport encryption middleware for the rest of the session

Current gameplay examples:

- `PlayerStatusHandler` listens to `PacketDefinition.GetPlayerStatusPacket`
  - `BasicStatus` requests enqueue `PlayerStatusPacket` (`0x11`)
  - `RequestSkills` requests enqueue `SkillListPacket` (`0x3A`)
- `ItemHandler` is now a thin router for item protocol traffic
  - `ItemBookService` handles `BookHeaderNewPacket` (`0xD4`) and `BookPagesPacket` (`0x66`)
  - `ItemInteractionService` handles `SingleClickPacket` (`0x09`) and `DoubleClickPacket` (`0x06`)
  - `ItemManipulationService` handles `PickUpItemPacket` (`0x07`), `DropItemPacket` (`0x08`), and `DropWearItemPacket` (`0x13`)
- `DyeWindowHandler` listens to `DyeWindowPacket` (`0x95`)
  - the classic dye-tub target flow opens `0x95` to the client
  - the client response on `0x95` applies hue to the pending item target
- `BulletinBoardHandler` listens to `BulletinBoardMessagesPacket` (`0x71`)
  - double click on a bulletin board opens the classic board window
  - client `sub 3/4/5/6` requests load messages, post replies, and remove owned leaf messages
- `HelpHandler` listens to `RequestHelpPacket` (`0x9B`)
  - delegates to `HelpRequestService`
  - bridges into Lua `on_help_request(session_id, character_id)`
  - opens the custom help gump wizard from `moongate_data/scripts/gumps/help.lua`
  - the wizard selects a ticket category, collects free text, then persists a `HelpTicket`
  - successful submit publishes `TicketOpenedEvent` and can be observed in Lua through `on_ticket_opened(event)`

## Pragmatic POL Coverage Matrix

This matrix tracks the packet subset that is already present in Moongate or still relevant for the current `7.x` client target.

`Status` values:

- `handler`: parsed and wired to gameplay with `RegisterPacketHandler(...)`
- `outgoing`: server-to-client packet emitted by runtime code
- `parse-only`: packet class is registered in `PacketRegistry`, but no gameplay listener handles it yet
- `missing`: relevant in POL/client docs, but not implemented in Moongate

| Opcode | Op Description | Direction | Moongate Packet Class | Status | Runtime Wiring | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `0xEF` | Login Seed | C -> S | `LoginSeedPacket` | `handler` | `LoginHandler` | Login bootstrap |
| `0x80` | Account Login | C -> S | `AccountLoginPacket` | `handler` | `LoginHandler` | Account auth |
| `0xA0` | Server Select | C -> S | `ServerSelectPacket` | `handler` | `LoginHandler` | Shard select |
| `0x91` | Game Login | C -> S | `GameLoginPacket` | `handler` | `LoginHandler` | Game-server auth |
| `0x5D` | Login Character | C -> S | `LoginCharacterPacket` | `handler` | `LoginHandler` | Character enter world |
| `0xBD` | Client Version | C -> S | `ClientVersionPacket` | `handler` | `LoginHandler` | Stores negotiated client version |
| `0xE1` | Client Type | C -> S | `ClientTypePacket` | `handler` | `LoginHandler`, `GeneralInformationHandler` | Stores session client capabilities for KR/SA/EC-aware flows |
| `0xF8` | Character Creation | C -> S | `CharacterCreationPacket` | `handler` | `CharacterHandler` | Modern creation flow |
| `0x72` | Request War Mode / War Mode | both | `RequestWarModePacket`, `WarModePacket` | `handler` + `outgoing` | `CharacterHandler` | Inbound toggle request and outbound war-mode state reply share opcode `0x72`; outgoing packet is intentionally not registry-decorated to avoid direction-agnostic opcode collision |
| `0x02` | Move Request | C -> S | `MoveRequestPacket` | `handler` | `MovementHandler` | Core movement |
| `0x73` | Ping Message | C -> S | `PingMessagePacket` | `handler` | `PingPongHandler` | Keepalive |
| `0xC8` | Client View Range | C -> S | `ClientViewRangePacket` | `handler` | `ClientViewRangeHandler` | View range update |
| `0x34` | Get Player Status | C -> S | `GetPlayerStatusPacket` | `handler` | `PlayerStatusHandler` | `BasicStatus -> 0x11`, `RequestSkills -> 0x3A` |
| `0xAD` | Unicode Speech | C -> S | `UnicodeSpeechPacket` | `handler` | `SpeechHandler` | In-game commands and world speech/emotes; `*text*` -> emote, `!text` -> yell, `;text` -> whisper |
| `0xB5` | Open Chat Window | C -> S | `OpenChatWindowPacket` | `handler` | `ChatHandler` | Opens classic conference chat and creates runtime chat user |
| `0xB3` | Chat Text | C -> S | `ChatTextPacket` | `handler` | `ChatHandler` | Conference chat actions (`message`, `join`, `pm`, `ignore`, `ops`, `voice`, `kick`, `whois`, `emote`) |
| `0x6C` | Target Cursor Commands | C -> S | `TargetCursorCommandsPacket` | `handler` | `PlayerTargetService` | Target callbacks |
| `0xBF` | General Information | C -> S | `GeneralInformationPacket` | `handler` | `GeneralInformationHandler` | Includes context menu / stat lock subcommands |
| `0xD6` | Mega Cliloc | C -> S | `MegaClilocPacket` | `handler` | `ToolTipHandler` | Tooltip requests |
| `0xB1` | Gump Menu Selection | C -> S | `GumpMenuSelectionPacket` | `handler` | `GumpHandler` | Gump button replies |
| `0x9B` | Request Help | C -> S | `RequestHelpPacket` | `handler` | `HelpHandler -> HelpRequestService` | Opens the Lua help-ticket wizard and submits persisted tickets through `help_tickets` |
| `0x05` | Request Attack | C -> S | `RequestAttackPacket` | `handler` | `RequestAttackHandler -> CombatService` | Sets combatant, forces warmode, and schedules the current weapon auto-attack flow (melee or ranged) |
| `0x06` | Double Click | C -> S | `DoubleClickPacket` | `handler` | `ItemHandler -> ItemInteractionService` | Item use / open flows |
| `0x09` | Single Click | C -> S | `SingleClickPacket` | `handler` | `ItemHandler -> ItemInteractionService` | Labels / tooltip-side behavior |
| `0x07` | Pick Up Item | C -> S | `PickUpItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | Drag start |
| `0x08` | Drop Item | C -> S | `DropItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | Container / world drop |
| `0x13` | Drop -> Wear Item | C -> S | `DropWearItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | Equip flow |
| `0x66` | Books (Pages) | C -> S | `BookPagesPacket` | `handler` | `ItemHandler -> ItemBookService` | Page request and writable page save |
| `0x71` | Bulletin Board Messages | both | `BulletinBoardMessagesPacket`, `BulletinBoardDisplayPacket`, `BulletinBoardSummaryPacket`, `BulletinBoardMessagePacket` | `handler` + `outgoing` | `BulletinBoardHandler`, `BulletinBoardModule` / `BulletinBoardService` | Classic bulletin board open/read/post/remove flow |
| `0x95` | Dye Window | both | `DyeWindowPacket`, `DisplayDyeWindowPacket` | `handler` + `outgoing` | `DyeWindowHandler`, `DyeModule` / `DyeColorService` | Classic dye tub hue picker flow; outgoing packet intentionally not registry-decorated to avoid opcode collision |
| `0xD4` | Book Header (New) | C -> S | `BookHeaderNewPacket` | `handler` | `ItemHandler -> ItemBookService` | Writable `title` / `author` save |
| `0xD9` | Spy On Client | C -> S | `SpyOnClientPacket` | `handler` | `PlayerHandler` | Minimal session-side handling |
| `0x03` | Talk Request | C -> S | `TalkRequestPacket` | `parse-only` | none | Legacy speech path not wired |
| `0x12` | Request Skill / Use | C -> S | `RequestSkillUsePacket` | `parse-only` | none | Skill-use flow still missing |
| `0x83` | Delete Character | C -> S | `DeleteCharacterPacket` | `parse-only` | none | No delete flow wired |
| `0xA8` | Server List | S -> C | `ServerListPacket` | `outgoing` | `LoginHandler` | Shard list |
| `0x8C` | Server Redirect | S -> C | `ServerRedirectPacket` | `outgoing` | `LoginHandler` | Redirect to game server |
| `0x1B` | Login Confirm | S -> C | `LoginConfirmPacket` | `outgoing` | login flow | Character accepted |
| `0xA9` | Characters / Starting Locations | S -> C | `CharactersStartingLocationsPacket` | `outgoing` | login flow | Character list + starts |
| `0xB9` | Support Features | S -> C | `SupportFeaturesPacket` | `outgoing` | login flow | Enables client features |
| `0x55` | Login Complete | S -> C | `LoginCompletePacket` | `outgoing` | login flow | Enter world complete |
| `0x22` | Move Confirm | S -> C | `MoveConfirmPacket` | `outgoing` | movement flow | Movement ack |
| `0x21` | Move Deny | S -> C | `MoveDenyPacket` | `outgoing` | movement flow | Movement reject |
| `0x78` | Mobile Incoming | S -> C | `MobileIncomingPacket` | `outgoing` | world/entity sync | Nearby mobiles |
| `0x20` | Draw Player | S -> C | `DrawPlayerPacket` | `outgoing` | login/move flow | Draw controlled mobile |
| `0x2E` | Worn Item | S -> C | `WornItemPacket` | `outgoing` | equipment sync | Equipped visuals |
| `0x24` | Draw Container | S -> C | `DrawContainerPacket` | `outgoing` | container flow | Open container |
| `0x3C` | Add Multiple Items To Container | S -> C | `AddMultipleItemsToContainerPacket` | `outgoing` | container flow | Batched contents |
| `0x88` | Paperdoll | S -> C | `PaperdollPacket` | `outgoing` | character UI | Paperdoll open with a computed fame/karma reputation title prefix loaded at startup from Lua config |
| `0x11` | Status Bar Info | S -> C | `PlayerStatusPacket` | `outgoing` | `PlayerStatusHandler` | Modern `7.x` status payload |
| `0x2F` | Fight Occuring | S -> C | `FightOccurringPacket` | `outgoing` | `CombatService` | Broadcast when a scheduled melee swing is attempted |
| `0xAA` | Allow/Refuse Attack | S -> C | `ChangeCombatantPacket` | `outgoing` | `CombatService` | Current combatant serial or `Serial.Zero` |
| `0xB2` | Chat Command | S -> C | `ChatCommandPacket` | `outgoing` | `ChatSystemService` | Classic conference chat responses and UI updates |
| `0x3A` | Send Skills | S -> C | `SkillListPacket` | `outgoing` | `PlayerStatusHandler`, `CombatService` | Full skill list with lock state, also reused after combat-driven skill gains |
| `0x23` | Dragging Of Item | S -> C | `DraggingOfItemPacket` | `outgoing` | item drag flow | Drag visual |
| `0xAE` | Unicode Speech Message | S -> C | `UnicodeSpeechMessagePacket` | `outgoing` | speech/system messages | Server speech |
| `0xB0` | Generic Gump | S -> C | `GenericGumpPacket` | `outgoing` | gump flow | Standard gump |
| `0xDD` | Compressed Gump | S -> C | `CompressedGumpPacket` | `outgoing` | gump flow | Compressed gump |
| `0xF3` | Object Information | S -> C | `ObjectInformationPacket` | `outgoing` | item/world sync | Item/object updates |
| `0x76` | Server Change | S -> C | `ServerChangePacket` | `outgoing` | map transition flow | Map/server boundary change |
| `0x65` | Weather | S -> C | `SetWeatherPacket` | `outgoing` | world presentation | Client 7.x still supports it |
| `0x54` | Play Sound Effect | S -> C | `PlaySoundEffectPacket` | `outgoing` | world presentation | Audio cue |
| `0x70` | Graphical Effect | S -> C | `GraphicalEffectPacket` | `outgoing` | world presentation | Classic effect |
| `0xC0` | Hued Effect | S -> C | `HuedEffectPacket` | `outgoing` | world presentation | Colored effect |
| `0xC7` | Particle Effect | S -> C | `ParticleEffectPacket` | `outgoing` | world presentation | Particle effect |
| `0x7C` | Relay | both | none | `missing` | none | Relevant in POL docs, not implemented in Moongate |
| `0x16` | Status Bar Info (old) | S -> C | none | `missing` | none | POL legacy status packet; Moongate uses modern `0x11` only |
| `0x39` | Group Remove / old chat | C -> S | none | `missing` | none | Not targeted for current client/runtime |

## Serialization

Outbound packets are serialized with `SpanWriter` in `OutboundPacketSender`.

- fixed packets typically pre-size buffer with `Length`
- variable packets can use dynamic writer growth
- packet logging can output hex dump when `LogPacketData` is enabled

Current notable outgoing packet classes:

- `PlayerStatusPacket`
  - modern `7.x` status layout
  - reads effective mobile state from `UOMobileEntity`
- `CharactersStartingLocationsPacket`
  - outgoing `0xA9`
  - sets the KR/UO3D-compatible flags when `session.IsEnhancedClient` is true
- `MobileIncomingPacket`
  - outgoing `0x78`
  - chooses the new mobile format from the recipient session capability instead of assuming one global client shape
- `PaperdollPacket`
  - outgoing `0x88`
  - serializes the paperdoll display name using the fame/karma reputation title table
  - the title table is loaded at startup from `moongate_data/scripts/config/reputation_titles_default.lua`
  - an optional `moongate_data/scripts/config/reputation_titles.lua` override can replace the default table
  - preserves the existing custom mobile `Title` as a suffix after the name
- `SkillListPacket`
  - outgoing `0x3A`
  - serializes the persisted mobile skill table in skill-id order
  - includes lock state, but not per-skill caps
- `BookHeaderNewPacket`
  - opens the classic book UI
  - toggles client writable mode per item

## Source Generation

`Moongate.Generators` provides generated packet artifacts used by runtime:

- packet table registration code
- packet opcode constants (`PacketDefinition` partial)

This avoids manual opcode duplication and keeps registration centralized.

## Listener Registration Generation

Server listener wiring is also source-generated.

Listener classes declare handled opcodes:

```csharp
[RegisterPacketHandler(PacketDefinition.MoveRequestPacket)]
public class MovementHandler : BasePacketListener
{
    // ...
}
```

`Moongate.Generators` produces bootstrap code that calls
`RegisterPacketHandler<TListener>(container, opCode)` for all discovered attributes.

This keeps:

- listener mapping explicit and compile-time validated.
- bootstrap code shorter and easier to maintain.
- runtime startup free from reflection-based handler scanning.

## Game Event Listener Registration Generation

In addition to packet handler registration, Moongate also generates game-event listener subscriptions.

Listener classes decorated with `[RegisterGameEventListener]` are scanned at compile time.
For each implemented `IGameEventListener<TEvent>`, generated bootstrap code subscribes the listener on `IGameEventBusService`.

---

**Previous**: [Protocol Reference](protocol.md)
