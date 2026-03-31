# POL Packet Coverage

Reference matrix comparing the POL packet catalog with Moongate's current runtime.

Source index:

- POL packet catalog: <https://docs.polserver.com/packets/index.php>

This page is intentionally broader than [Packet System](packets.md).
It is meant for gap analysis against the POL packet catalog, not just for documenting the packets currently used by Moongate gameplay.

`Status` values:

- `handler`: parsed and wired to gameplay with a registered packet listener
- `outgoing`: emitted by Moongate runtime to the client
- `parse-only`: packet class exists in Moongate, but no gameplay listener handles it yet
- `missing`: present in POL docs but not currently implemented in Moongate

`POL Page` values use the packet-specific POL documentation URL shape:

- `https://docs.polserver.com/packets/index.php?Packet=0xXX`

## Implemented

| Opcode | Op Description | Direction | POL Page | Moongate Packet Class | Status | Runtime Wiring | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `0x02` | Move request | C -> S | `?Packet=0x02` | `MoveRequestPacket` | `handler` | `MovementHandler` | Core player movement |
| `0x06` | Double click | C -> S | `?Packet=0x06` | `DoubleClickPacket` | `handler` | `ItemHandler -> ItemInteractionService` | Item use and open flows |
| `0x07` | Pick up item | C -> S | `?Packet=0x07` | `PickUpItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | Drag start |
| `0x08` | Drop item | C -> S | `?Packet=0x08` | `DropItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | World/container drop |
| `0x09` | Single click | C -> S | `?Packet=0x09` | `SingleClickPacket` | `handler` | `ItemHandler -> ItemInteractionService` | Labels and inspect-style flows |
| `0x11` | Status bar info | S -> C | `?Packet=0x11` | `PlayerStatusPacket` | `outgoing` | `PlayerStatusHandler` | Modern `7.x` status packet |
| `0x13` | Drop to wear | C -> S | `?Packet=0x13` | `DropWearItemPacket` | `handler` | `ItemHandler -> ItemManipulationService` | Equip flow |
| `0x1B` | Login confirm | S -> C | `?Packet=0x1B` | `LoginConfirmPacket` | `outgoing` | login flow | Character accepted |
| `0x20` | Draw player | S -> C | `?Packet=0x20` | `DrawPlayerPacket` | `outgoing` | login and move flows | Controlled mobile draw |
| `0x21` | Move reject | S -> C | `?Packet=0x21` | `MoveDenyPacket` | `outgoing` | movement flow | Reject or resync |
| `0x22` | Move ack | both | `?Packet=0x22` | `MoveConfirmPacket` | `outgoing` | movement flow | Moongate currently only emits server response |
| `0x23` | Dragging item | S -> C | `?Packet=0x23` | `DraggingOfItemPacket` | `outgoing` | item drag flow | Visual drag state |
| `0x24` | Draw container | S -> C | `?Packet=0x24` | `DrawContainerPacket` | `outgoing` | container flow | Container open packet |
| `0x2E` | Worn item | S -> C | `?Packet=0x2E` | `WornItemPacket` | `outgoing` | equipment sync | Equipped visuals |
| `0x34` | Get player status | C -> S | `?Packet=0x34` | `GetPlayerStatusPacket` | `handler` | `PlayerStatusHandler` | `BasicStatus -> 0x11`, `RequestSkills -> 0x3A` |
| `0x3A` | Send skills | both | `?Packet=0x3A` | `SkillListPacket` | `outgoing` | `PlayerStatusHandler`, `CombatService` | Moongate sends the full skill list on explicit requests and after combat-driven skill gains/stat progression; no inbound listener |
| `0x3C` | Add multiple items to container | S -> C | `?Packet=0x3C` | `AddMultipleItemsToContainerPacket` | `outgoing` | container flow | Batched container contents |
| `0x4E` | Personal light level | S -> C | `?Packet=0x4E` | `PersonalLightLevelPacket` | `outgoing` | world presentation | Client light state |
| `0x4F` | Overall light level | S -> C | `?Packet=0x4F` | `OverallLightLevelPacket` | `outgoing` | world presentation | Global light state |
| `0x54` | Play sound effect | S -> C | `?Packet=0x54` | `PlaySoundEffectPacket` | `outgoing` | world presentation | Audio cue |
| `0x55` | Login complete | S -> C | `?Packet=0x55` | `LoginCompletePacket` | `outgoing` | login flow | End of login handshake |
| `0x5B` | Time | S -> C | `?Packet=0x5B` | `SetTimePacket` | `outgoing` | world presentation | World time |
| `0x5D` | Login character | C -> S | `?Packet=0x5D` | `LoginCharacterPacket` | `handler` | `LoginHandler` | Character enters world |
| `0x65` | Weather | S -> C | `?Packet=0x65` | `SetWeatherPacket` | `outgoing` | world presentation | Weather state |
| `0x66` | Book pages | both | `?Packet=0x66` | `BookPagesPacket` | `handler` | `ItemHandler -> ItemBookService` | Page request and writable page save |
| `0x6C` | Target cursor commands | both | `?Packet=0x6C` | `TargetCursorCommandsPacket` | `handler` | `PlayerTargetService` | Inbound target replies; outbound cursor flows use same opcode family in protocol |
| `0x6D` | Play music | S -> C | `?Packet=0x6D` | `SetMusicPacket` | `outgoing` | world presentation | Music trigger |
| `0x6E` | Character animation | S -> C | `?Packet=0x6E` | `MobileAnimationPacket` | `outgoing` | world presentation | Mobile anims |
| `0x70` | Graphical effect | S -> C | `?Packet=0x70` | `GraphicalEffectPacket` | `outgoing` | world presentation | Classic effect |
| `0x72` | War mode toggle/state | both | `?Packet=0x72` | `RequestWarModePacket`, `WarModePacket` | `handler` + `outgoing` | `CharacterHandler` | Inbound toggle request and outbound state reply share opcode |
| `0x73` | Ping | C -> S | `?Packet=0x73` | `PingMessagePacket` | `handler` | `PingPongHandler` | Keepalive |
| `0x76` | Server change | S -> C | `?Packet=0x76` | `ServerChangePacket` | `outgoing` | map transition flow | Subserver/map boundary |
| `0x78` | Draw object/mobile incoming | S -> C | `?Packet=0x78` | `MobileIncomingPacket` | `outgoing` | world/entity sync | Nearby mobiles |
| `0x80` | Account login | C -> S | `?Packet=0x80` | `AccountLoginPacket` | `handler` | `LoginHandler` | Account auth |
| `0x82` | Login denied | S -> C | `?Packet=0x82` | `LoginDeniedPacket` | `outgoing` | login flow | Failed auth |
| `0x88` | Paperdoll | S -> C | `?Packet=0x88` | `PaperdollPacket` | `outgoing` | character UI | Open paperdoll |
| `0x8C` | Server redirect | S -> C | `?Packet=0x8C` | `ServerRedirectPacket` | `outgoing` | `LoginHandler` | Redirect to game server |
| `0x91` | Game login | C -> S | `?Packet=0x91` | `GameLoginPacket` | `handler` | `LoginHandler` | Game-server auth |
| `0x97` | Move player | S -> C | `?Packet=0x97` | `MovePlayerPacket` | `outgoing` | movement flow | Server-driven player move |
| `0xA0` | Select server | C -> S | `?Packet=0xA0` | `ServerSelectPacket` | `handler` | `LoginHandler` | Shard select |
| `0xA8` | Server list | S -> C | `?Packet=0xA8` | `ServerListPacket` | `outgoing` | `LoginHandler` | Shard list |
| `0xA9` | Characters / starting locations | S -> C | `?Packet=0xA9` | `CharactersStartingLocationsPacket` | `outgoing` | login flow | Character list and starts |
| `0xAD` | Unicode speech request | C -> S | `?Packet=0xAD` | `UnicodeSpeechPacket` | `handler` | `SpeechHandler` | In-game speech and command path |
| `0xAE` | Unicode speech message | S -> C | `?Packet=0xAE` | `UnicodeSpeechMessagePacket` | `outgoing` | speech and system messages | Server speech |
| `0xB0` | Generic gump | S -> C | `?Packet=0xB0` | `GenericGumpPacket` | `outgoing` | gump flow | Standard gump |
| `0xB1` | Gump menu selection | C -> S | `?Packet=0xB1` | `GumpMenuSelectionPacket` | `handler` | `GumpHandler` | Gump replies |
| `0xB5` | Open chat window | C -> S | `?Packet=0xB5` | `OpenChatWindowPacket` | `handler` | `ChatHandler` | Chat open and runtime user bootstrap |
| `0xB9` | Enable locked client features | S -> C | `?Packet=0xB9` | `SupportFeaturesPacket` | `outgoing` | login flow | Client feature flags |
| `0xBC` | Season | S -> C | `?Packet=0xBC` | `SeasonPacket` | `outgoing` | world presentation | Season state |
| `0xBD` | Client version | C -> S | `?Packet=0xBD` | `ClientVersionPacket` | `handler` | `LoginHandler` | Stores client version |
| `0xBF` | General information | C -> S | `?Packet=0xBF` | `GeneralInformationPacket` | `handler` | `GeneralInformationHandler` | Context menus, persisted stat-lock updates, targeted actions |
| `0xC0` | Hued effect | S -> C | `?Packet=0xC0` | `HuedEffectPacket` | `outgoing` | world presentation | Colored effect |
| `0xC7` | Particle effect | S -> C | `?Packet=0xC7` | `ParticleEffectPacket` | `outgoing` | world presentation | Particle effect |
| `0xC8` | Client view range | C -> S | `?Packet=0xC8` | `ClientViewRangePacket` | `handler` | `ClientViewRangeHandler` | View range update |
| `0xD6` | Mega cliloc | C -> S | `?Packet=0xD6` | `MegaClilocPacket` | `handler` | `ToolTipHandler` | Tooltip requests |
| `0xD9` | Spy on client | C -> S | `?Packet=0xD9` | `SpyOnClientPacket` | `handler` | `PlayerHandler` | Minimal handling only |
| `0xDD` | Compressed gump | S -> C | `?Packet=0xDD` | `CompressedGumpPacket` | `outgoing` | gump flow | Compressed gump |
| `0xEF` | Login seed | C -> S | `?Packet=0xEF` | `LoginSeedPacket` | `handler` | `LoginHandler` | Login bootstrap |
| `0xF2` | Time sync response | S -> C | `?Packet=0xF2` | `TimeSyncResponsePacket` | `outgoing` | movement/time flow | Time sync reply |
| `0xF3` | Object information | S -> C | `?Packet=0xF3` | `ObjectInformationPacket` | `outgoing` | item/world sync | Item/object update |
| `0xF8` | Character creation | C -> S | `?Packet=0xF8` | `CharacterCreationPacket` | `handler` | `CharacterHandler` | Current modern create flow |

## Parse-only

| Opcode | Op Description | Direction | POL Page | Moongate Packet Class | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `0x00` | Legacy create character | C -> S | `?Packet=0x00` | `CreateCharacterPacket` | `parse-only` | Legacy character creation path is not wired |
| `0x01` | Disconnect notification | C -> S | `?Packet=0x01` | `DisconnectNotificationPacket` | `parse-only` | Session shutdown is not modeled via gameplay listener |
| `0x03` | Talk request | C -> S | `?Packet=0x03` | `TalkRequestPacket` | `parse-only` | Legacy speech path not wired; Unicode path is used |
| `0x05` | Request attack | C -> S | `?Packet=0x05` | `RequestAttackPacket` | `handler` | `RequestAttackHandler -> CombatService` | Sets combatant, enters warmode, and schedules the weapon-driven auto-attack loop (melee or ranged) |
| `0x12` | Skill or action use request | C -> S | `?Packet=0x12` | `RequestSkillUsePacket` | `parse-only` | Skill-use flow still missing |
| `0x2C` | Resurrection menu | both | `?Packet=0x2C` | `ResurrectionMenuPacket` | `parse-only` | No resurrect handler yet |
| `0x3B` | Buy items | C -> S | `?Packet=0x3B` | `BuyItemsPacket` | `parse-only` | Vendor buy flow missing |
| `0x56` | Map packet | both | `?Packet=0x56` | `MapPacket` | `parse-only` | Cartography/treasure flow missing |
| `0x6F` | Secure trading | both | `?Packet=0x6F` | `SecureTradingPacket` | `parse-only` | Trade flow missing |
| `0x71` | Bulletin board messages | both | `?Packet=0x71` | `BulletinBoardMessagesPacket` | `parse-only` | Bulletin boards not wired |
| `0x75` | Rename character | C -> S | `?Packet=0x75` | `RenameCharacterPacket` | `parse-only` | Rename flow missing |
| `0x7D` | Dialog response | C -> S | `?Packet=0x7D` | `DialogResponsePacket` | `parse-only` | Legacy dialog response not wired |
| `0x83` | Delete character | C -> S | `?Packet=0x83` | `DeleteCharacterPacket` | `parse-only` | Delete flow missing |
| `0x95` | Dye window | both | `?Packet=0x95` | `DyeWindowPacket`, `DisplayDyeWindowPacket` | `implemented` | Classic dye tub flow wired through `DyeColorService` and Lua `dye` module |
| `0x98` | All names | C -> S | `?Packet=0x98` | `AllNamesPacket` | `parse-only` | 3D all-names flow missing |
| `0x9A` | Console entry prompt | C -> S | `?Packet=0x9A` | `ConsoleEntryPromptPacket` | `parse-only` | No gameplay listener |
| `0x9B` | Request help | C -> S | `?Packet=0x9B` | `RequestHelpPacket` | `handler` | `HelpHandler -> HelpRequestService -> Lua on_help_request / gumps.help` | Opens the Lua help-ticket wizard, persists a `HelpTicket`, and emits `TicketOpenedEvent` |
| `0x9F` | Sell list reply | C -> S | `?Packet=0x9F` | `SellListReplyPacket` | `parse-only` | Vendor sell flow missing |
| `0xA4` | Client spy | C -> S | `?Packet=0xA4` | `ClientSpyPacket` | `parse-only` | No behavior attached |
| `0xA7` | Request tip / notice window | C -> S | `?Packet=0xA7` | `RequestTipNoticeWindowPacket` | `parse-only` | Tip flow missing |
| `0xB3` | Chat text | C -> S | `?Packet=0xB3` | `ChatTextPacket` | `handler` | ModernUO-style conference chat action dispatch via `IChatSystemService` |
| `0xB6` | Help / tip request | C -> S | `?Packet=0xB6` | `SendHelpTipRequestPacket` | `parse-only` | Tip flow missing |
| `0xB8` | Character profile request | C -> S | `?Packet=0xB8` | `RequestCharProfilePacket` | `handler` | `CharacterProfileHandler` | Player-only profile display and self-edit flow with lock and account-age footer |
| `0xBE` | Assist version | C -> S | `?Packet=0xBE` | `AssistVersionPacket` | `parse-only` | No behavior attached |
| `0xC2` | Unicode text entry | C -> S | `?Packet=0xC2` | `UnicodeTextEntryPacket` | `parse-only` | Text entry flow missing |
| `0xD0` | Configuration file | C -> S | `?Packet=0xD0` | `ConfigurationFilePacket` | `parse-only` | No behavior attached |
| `0xD1` | Logout status | C -> S | `?Packet=0xD1` | `LogoutStatusPacket` | `parse-only` | No dedicated logout listener |
| `0xD4` | Book header new | C -> S | `?Packet=0xD4` | `BookHeaderNewPacket` | `handler` | `ItemHandler -> ItemBookService` | Writable `title` / `author` save for client `7.x` |
| `0xD7` | Generic AOS command family | C -> S | `?Packet=0xD7` | `QuestGumpRequestPacket` | `handler` | `QuestGumpRequestHandler` | Encoded envelope; quest journal leaf `0x0032` requires payload `[0x07]` |
| `0xE1` | Client type | C -> S | `?Packet=0xE1` | `ClientTypePacket` | `handler` | `LoginHandler` | Stores session client capability and also accepts the Enhanced Client variant that carries the version string; runtime then drives `0x78` sync shape |
| `0xEC` | Equip macro | C -> S | `?Packet=0xEC` | `EquipMacroPacket` | `parse-only` | KR macro flow missing |
| `0xED` | Unequip macro | C -> S | `?Packet=0xED` | `UnequipItemMacroPacket` | `parse-only` | KR macro flow missing |
| `0xF0` | KR movement request | C -> S | `?Packet=0xF0` | `NewMovementRequestPacket` | `parse-only` | Alternate movement path not wired |
| `0xF1` | Freeshard list | C -> S | `?Packet=0xF1` | `FreeshardListPacket` | `parse-only` | No behavior attached |
| `0xF4` | Crash report | C -> S | `?Packet=0xF4` | `CrashReportPacket` | `parse-only` | Report intake missing |
| `0xFA` | Open UO Store | C -> S | `?Packet=0xFA` | `OpenUoStorePacket` | `parse-only` | Store flow missing |
| `0xFB` | Update view public house contents | C -> S | `?Packet=0xFB` | `UpdateViewPublicHouseContentsPacket` | `parse-only` | House-content flow missing |

## Missing

These packets appear in the POL catalog but are not currently implemented in Moongate.
This section intentionally includes older or lower-priority packets so opcode ranges like `0x30` are visible during gap analysis.

| Opcode | Op Description | Direction | POL Page | Status | Notes |
| --- | --- | --- | --- | --- | --- |
| `0x0B` | Damage | S -> C | `?Packet=0x0B` | `missing` | No dedicated damage packet yet |
| `0x0C` | Edit tile data | both | `?Packet=0x0C` | `missing` | God-client tooling not targeted |
| `0x15` | Follow | both | `?Packet=0x15` | `missing` | Legacy follow flow absent |
| `0x16` | New health-bar status update | S -> C | `?Packet=0x16` | `missing` | Moongate uses `0x11` only |
| `0x17` | Health-bar status update | S -> C | `?Packet=0x17` | `missing` | KR legacy status variant absent |
| `0x1A` | Object info | S -> C | `?Packet=0x1A` | `missing` | Not used; Moongate uses `0xF3`/modern object packets |
| `0x1C` | Send speech | S -> C | `?Packet=0x1C` | `missing` | Speech is sent through `0xAE` |
| `0x1D` | Delete object | S -> C | `?Packet=0x1D` | `missing` | Remove-object packet not modeled separately |
| `0x1F` | Explosion | S -> C | `?Packet=0x1F` | `missing` | Effect coverage uses newer packets |
| `0x25` | Add item to container | S -> C | `?Packet=0x25` | `missing` | Moongate currently favors batched container payloads |
| `0x26` | Kick player | S -> C | `?Packet=0x26` | `missing` | No explicit kick packet emitter |
| `0x27` | Reject move item request | S -> C | `?Packet=0x27` | `missing` | Drag/drop rejection flow not implemented |
| `0x28` | Drop item failed | S -> C | `?Packet=0x28` | `missing` | Clear-square or failure response absent |
| `0x29` | Drop item approved | S -> C | `?Packet=0x29` | `missing` | Explicit approval packet absent |
| `0x2A` | Blood | S -> C | `?Packet=0x2A` | `missing` | No blood effect packet |
| `0x2B` | God mode / path packet family | S -> C | `?Packet=0x2B` | `missing` | Not targeted |
| `0x2D` | Mobile attributes | S -> C | `?Packet=0x2D` | `missing` | No dedicated mob-attributes packet |
| `0x2F` | Fight occurring | S -> C | `?Packet=0x2F` | `FightOccurringPacket` | `outgoing` | `CombatService` | Sent when a scheduled melee swing is attempted |
| `0x30` | Attack ok | S -> C | `?Packet=0x30` | `missing` | Combat target confirmation not implemented |
| `0x31` | Attack ended | S -> C | `?Packet=0x31` | `missing` | Combat target clear not implemented |
| `0x32` | Legacy unknown | S -> C | `?Packet=0x32` | `missing` | Left undocumented/unused in Moongate |
| `0x33` | Pause client | S -> C | `?Packet=0x33` | `missing` | Pause flow absent |
| `0x35` | Add resource | C -> S | `?Packet=0x35` | `missing` | God-client tooling not targeted |
| `0x36` | Resource tile data | C -> S | `?Packet=0x36` | `missing` | God-client tooling not targeted |
| `0x37` | Move item (god client) | C -> S | `?Packet=0x37` | `missing` | God-client tooling not targeted |
| `0x38` | Pathfinding in client | C -> S | `?Packet=0x38` | `missing` | No path packet flow yet |
| `0x39` | Remove group | both | `?Packet=0x39` | `missing` | Legacy group/chat packet absent |
| `0x3E` | Versions | S -> C | `?Packet=0x3E` | `missing` | God-client tooling not targeted |
| `0x3F` | Update statics | S -> C | `?Packet=0x3F` | `missing` | God-client tooling not targeted |
| `0x45` | Version ok | both | `?Packet=0x45` | `missing` | Legacy version exchange absent |
| `0x46` | New artwork | S -> C | `?Packet=0x46` | `missing` | Patch/distribution flow absent |
| `0x47` | New terrain | S -> C | `?Packet=0x47` | `missing` | Patch/distribution flow absent |
| `0x48` | New animation | S -> C | `?Packet=0x48` | `missing` | Patch/distribution flow absent |
| `0x49` | New hues | S -> C | `?Packet=0x49` | `missing` | Patch/distribution flow absent |
| `0x4A` | Delete art | S -> C | `?Packet=0x4A` | `missing` | Patch/distribution flow absent |
| `0x4B` | Check client version | S -> C | `?Packet=0x4B` | `missing` | Not used by current handshake |
| `0x4C` | Script names | S -> C | `?Packet=0x4C` | `missing` | God-client tooling not targeted |
| `0x4D` | Edit script file | S -> C | `?Packet=0x4D` | `missing` | God-client tooling not targeted |
| `0x50` | Board header | S -> C | `?Packet=0x50` | `missing` | Bulletin board flow absent |
| `0x51` | Board message | S -> C | `?Packet=0x51` | `missing` | Bulletin board flow absent |
| `0x52` | Board post/remove | C -> S | `?Packet=0x52` | `missing` | Bulletin board flow absent |
| `0x53` | Reject character logon | S -> C | `?Packet=0x53` | `missing` | Moongate uses `0x82` for denial |
| `0x57` | Update regions | S -> C | `?Packet=0x57` | `missing` | Legacy region update absent |
| `0x58` | Add region | S -> C | `?Packet=0x58` | `missing` | Legacy region update absent |
| `0x59` | New context FX / terrain data | S -> C | `?Packet=0x59` | `missing` | Not modeled |
| `0x5A` | RunUO/POL legacy flow | S -> C | `?Packet=0x5A` | `missing` | Not modeled |
| `0x5C` | Server list entry extras | S -> C | `?Packet=0x5C` | `missing` | Not modeled |
| `0x5E` | Login character alt | C -> S | `?Packet=0x5E` | `missing` | Legacy variant absent |
| `0x5F` | Server list add entry | S -> C | `?Packet=0x5F` | `missing` | Dynamic server list mutation absent |
| `0x60` | Server list remove entry | S -> C | `?Packet=0x60` | `missing` | Dynamic server list mutation absent |
| `0x61` | Weather alt / bolt family | S -> C | `?Packet=0x61` | `missing` | Not modeled |
| `0x62` | Combat or movement legacy family | S -> C | `?Packet=0x62` | `missing` | Not modeled |
| `0x63` | Area weather / map family | S -> C | `?Packet=0x63` | `missing` | Not modeled |
| `0x64` | Particle or map legacy family | S -> C | `?Packet=0x64` | `missing` | Not modeled |
| `0x67` | New character animation family | S -> C | `?Packet=0x67` | `missing` | Not modeled |
| `0x68` | New target / map family | S -> C | `?Packet=0x68` | `missing` | Not modeled |
| `0x69` | Character animation alt | S -> C | `?Packet=0x69` | `missing` | Not modeled |
| `0x74` | Open buy window | S -> C | `?Packet=0x74` | `VendorBuyListPacket` | Classic vendor buy list implemented and used with `0x3C` + existing `DrawContainerPacket (0x24)` shop flow |
| `0x77` | Update player | S -> C | `?Packet=0x77` | `missing` | Moongate uses other draw/move packets |
| `0x7C` | Open dialog box | both | `?Packet=0x7C` | `missing` | Dialog box flow absent |
| `0x86` | Resend characters after delete | S -> C | `?Packet=0x86` | `missing` | Character delete flow absent |
| `0x89` | Corpse clothing | S -> C | `?Packet=0x89` | `missing` | Corpse clothing packet absent |
| `0x8D` | KR/SA character creation | C -> S | `?Packet=0x8D` | `missing` | Moongate uses `0xF8` modern create path |
| `0x90` | Display map | S -> C | `?Packet=0x90` | `missing` | Map display flow absent |
| `0x99` | Multi-placement or GM packet | S -> C | `?Packet=0x99` | `missing` | Not modeled |
| `0x9C` | Prompt response | S -> C | `?Packet=0x9C` | `missing` | Prompt flow absent |
| `0x9E` | Vendor descriptions | S -> C | `?Packet=0x9E` | `VendorSellListPacket` | Classic vendor sell list implemented for context-menu sell flow |
| `0xA1` | Attack cursor | S -> C | `?Packet=0xA1` | `missing` | Combat targeting UI absent |
| `0xA2` | Character animation extended | S -> C | `?Packet=0xA2` | `missing` | Not modeled |
| `0xA3` | Client prompt / speech family | S -> C | `?Packet=0xA3` | `missing` | Not modeled |
| `0xA5` | Open web browser | S -> C | `?Packet=0xA5` | `missing` | Not used |
| `0xA6` | Tip window | S -> C | `?Packet=0xA6` | `missing` | Tip UI absent |
| `0xAA` | Allow/refuse attack | S -> C | `?Packet=0xAA` | `ChangeCombatantPacket` | `outgoing` | `CombatService` | Current combatant serial or `Serial.Zero` |
| `0xAB` | Text entry dialog | S -> C | `?Packet=0xAB` | `missing` | Text entry flow absent |
| `0xAC` | Server list alt / game family | S -> C | `?Packet=0xAC` | `missing` | Not modeled |
| `0xAF` | Death animation | S -> C | `?Packet=0xAF` | `missing` | Packet class exists in Moongate, but no documented runtime flow yet |
| `0xB2` | Chat request | S -> C | `?Packet=0xB2` | `ChatCommandPacket` | `outgoing` | Conference chat responses/messages; implemented against ModernUO-style runtime chat rather than POL core |
| `0xB7` | Char profile reply | S -> C | `?Packet=0xB7` | `missing` | No profile reply flow |
| `0xBA` | Seasons alt / map family | S -> C | `?Packet=0xBA` | `missing` | Not modeled |
| `0xBB` | Ultima Messenger | both | `?Packet=0xBB` | `missing` | Messenger system absent |
| `0xC1` | Cliloc message | S -> C | `?Packet=0xC1` | `missing` | Direct cliloc message packet absent |
| `0xC4` | Semivisible update | S -> C | `?Packet=0xC4` | `missing` | Not modeled |
| `0xC5` | Invalid map enable | S -> C | `?Packet=0xC5` | `missing` | Not modeled |
| `0xC6` | Mega cliloc system message | S -> C | `?Packet=0xC6` | `missing` | Not modeled |
| `0xC9` | God-client packet | both | `?Packet=0xC9` | `missing` | Not targeted |
| `0xCA` | User server ping | both | `?Packet=0xCA` | `missing` | Not targeted |
| `0xCB` | Localized text / AOS system | S -> C | `?Packet=0xCB` | `missing` | Not modeled |
| `0xCC` | Cliloc message affix | S -> C | `?Packet=0xCC` | `missing` | Not modeled |
| `0xD2` | Extended stats / mobile update | S -> C | `?Packet=0xD2` | `missing` | Not modeled |
| `0xD3` | Show housing menu / house customize | S -> C | `?Packet=0xD3` | `missing` | Not modeled |
| `0xD8` | Custom house packet family | both | `?Packet=0xD8` | `missing` | Not modeled |
| `0xDB` | Mahjong system alt | S -> C | `?Packet=0xDB` | `missing` | Not modeled |
| `0xDC` | General house / compressed family | S -> C | `?Packet=0xDC` | `missing` | Not modeled |
| `0xDE` | Expanded text / tooltip family | S -> C | `?Packet=0xDE` | `missing` | Not modeled |
| `0xDF` | Buff/debuff or expansion packet | S -> C | `?Packet=0xDF` | `missing` | Not modeled |
| `0xE0` | KR encrypted payload family | both | `?Packet=0xE0` | `missing` | Not modeled |
| `0xE2` | New animation list / movement family | S -> C | `?Packet=0xE2` | `missing` | Not modeled |
| `0xE3` | KR map / object family | S -> C | `?Packet=0xE3` | `missing` | Not modeled |
| `0xF5` | New map message / expansion family | S -> C | `?Packet=0xF5` | `missing` | Not modeled |

## Notes

- `Moongate.Network.Packets` registration is direction-agnostic today, so identical inbound/outbound opcodes cannot both use `[PacketHandler(...)]` on different classes without colliding in the registry.
- The war-mode response packet `0x72` is a live example of that constraint: the outgoing class exists, but it is intentionally not registry-decorated.
- `packets.md` remains the runtime-oriented reference. This page is the broader comparison sheet against the POL catalog.

---

**Previous**: [Packet System](packets.md) | **Next**: [Protocol Reference](protocol.md)
| `0x71` | Bulletin Board Messages | both | `BulletinBoardMessagesPacket`, `BulletinBoardDisplayPacket`, `BulletinBoardSummaryPacket`, `BulletinBoardMessagePacket` | `implemented` | Classic board open, summary, message read, post, and owner-only leaf delete |
