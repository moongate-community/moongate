# Chat System Service Design

## Goal

Implement the classic UO chat conference system around packet `0xB3` with a dedicated runtime service, leaving world speech in `ISpeechService` and keeping chat state in-memory only.

## Why This Needs Its Own Service

The current codebase already has the packet edges for chat:

- `0xB5` open chat window
- `0xB2` outgoing chat command
- `0xB3` chat text/action packet stub

What is missing is the stateful runtime model behind those packets. That model does not fit `ISpeechService`, because chat conference behavior is not proximity speech. It needs:

- session-bound chat users
- named channels/conferences
- moderator and voice permissions
- ignore and private-message toggles
- per-channel password and rename state

The implementation should therefore introduce a new `IChatSystemService` and keep `ISpeechService` focused on world speech, NPC hearing, and command speech.

## Reference Model

POL is not the right source for this feature. POL handles `0xB5` only and delegates the chat button to script; it does not implement `0xB3` conference chat in core.

ModernUO is the right reference. It models chat as:

- a runtime chat user per connected session
- runtime channels
- `0xB5` to open the window
- `0xB3` parsed as:
  - language
  - action id
  - unicode parameter
- action dispatch in code rather than separate packet classes for every subtype
- `0xB2` server responses

Moongate should follow that structure.

## Scope

The first implementation should support the full command set the user requested:

- open chat window
- close chat
- join conference
- create new conference
- rename conference
- set/change conference password
- send conference message
- private message
- ignore / stop ignore / toggle ignore
- grant/remove/toggle voice
- grant/remove/toggle moderator
- PM receive off / on / toggle
- show character name off / on / toggle
- whois
- kick
- default speaking privilege off / on / toggle
- emote

This is still runtime-only. No persistence is required for users, channels, or message history.

## Architecture

### `IChatSystemService`

`IChatSystemService` owns all conference-chat runtime behavior:

- open chat window for a session
- create or resolve the session chat user
- parse and dispatch `0xB3` actions
- manage channels and membership
- enforce permission checks
- send `0xB2` responses/messages
- clean up users and channels when sessions leave

It should not own world speech, NPC hearing, or command parsing.

### Runtime Models

Introduce two runtime-only state objects.

#### `ChatUserState`

Holds session and user-specific chat state:

- `SessionId`
- `CharacterId`
- `CharacterName`
- `CurrentChannelName`
- `ReceivePrivateMessages`
- `ShowCharacterName`
- `IgnoredUsers`

Per-channel privileges such as moderator and voice should be stored with the channel membership, not globally on the user.

#### `ChatChannelState`

Holds conference state:

- `Name`
- optional `Password`
- `Members`
- `DefaultVoiceMode`
- empty-channel cleanup behavior

Membership should carry:

- `SessionId`
- `IsModerator`
- `CanSpeak`

This keeps channel authority localized to the channel and avoids global role leakage.

## Packet Handling

### `0xB5 Open Chat Window`

Current behavior sends `OpenChatWindow` through `ChatCommandPacket`. That should move behind `IChatSystemService.OpenWindowAsync(...)`.

Open behavior:

1. resolve/create `ChatUserState`
2. derive chat display name from character name
3. send `0xB2` open-window command
4. keep the user available for subsequent `0xB3` actions

### `0xB3 Chat Text`

Moongate should parse it exactly as the ModernUO-style generic action packet:

- 4-byte language code
- 2-byte action id
- one Unicode payload string

The payload stays raw at the packet layer. The service interprets it according to `action id`.

This avoids building dozens of packet classes for subcommands and matches actual client behavior.

### `0xB2 Chat Command`

Reuse the existing `ChatCommandPacket` as the server response transport.

If current `ChatCommandType` values are incomplete, extend them there rather than inventing a second outgoing chat packet family.

## Action Dispatch

`IChatSystemService` should dispatch by action id. The first implementation should support:

- `0x41` change password
- `0x58` close
- `0x61` message
- `0x62` join conference
- `0x63` create new conference
- `0x64` rename conference
- `0x65` private message
- `0x66` ignore
- `0x67` stop ignore
- `0x68` toggle ignore
- `0x69` grant voice
- `0x6A` remove voice
- `0x6B` toggle voice
- `0x6C` grant moderator
- `0x6D` remove moderator
- `0x6E` toggle moderator
- `0x6F` disable PM receive
- `0x70` enable PM receive
- `0x71` toggle PM receive
- `0x72` show character name
- `0x73` hide character name
- `0x74` toggle character name display
- `0x75` whois
- `0x76` kick
- `0x77` moderators-only voice default
- `0x78` everyone-can-speak default
- `0x79` toggle default voice behavior
- `0x7A` emote

Unsupported or malformed requests should be consumed and translated into a server-side system response rather than warning spam.

## Parsing Rules for Rich Commands

The packet documentation shows several rich subformats, but ModernUO proves the client can be handled with a single Unicode parameter string and action-specific parsing.

Moongate should mirror that:

- join conference parses quoted channel name and optional password from the string
- create conference parses optional password markers from the string
- PM, ignore, voice, ops, whois, kick parse the target username from the string
- emote uses the string as body text

This keeps packet parsing simple and pushes command semantics into chat logic where they belong.

## Permission Model

### Channel Operations

These actions require moderator privileges in the current channel:

- rename conference
- change password
- grant/remove/toggle voice
- grant/remove/toggle moderator
- kick
- default speaking privilege changes

The service should reject those actions with a chat system response if the caller lacks moderator status.

### Speaking Rules

Speaking is allowed when:

- the user is a moderator, or
- channel default voice mode allows everyone to speak, or
- the user has explicit voice permission

This should apply to both normal channel message and emote.

## Identity and Privacy Rules

### Private Messages

Private messages should only be delivered if:

- sender and recipient exist in chat
- recipient is not ignoring sender
- recipient allows private messages

Otherwise the sender should receive a system response explaining the failure.

### `whois`

`whois` should respect the target user's character-name visibility:

- if enabled, return the visible character name
- if disabled, return a generic hidden/unavailable response

This keeps the toggle meaningful.

## Lifecycle

When a chat user closes the chat window or disconnects:

- remove the user from its current channel
- notify affected users if needed through chat system responses
- delete the channel if it becomes empty

This avoids stale runtime state.

## Integration Points

### Packet Handlers

Add a dedicated chat handler rather than expanding `SpeechHandler` further. `SpeechHandler` should retain:

- `UnicodeSpeechPacket`
- possibly `OpenChatWindowPacket` only if delegated immediately

`ChatTextPacket` should be owned by the new chat handler.

### Session Cleanup

The chat system needs a cleanup hook when a session disconnects. If the project already has a session-ended event path, the chat service should subscribe or be called from there instead of polling.

### Logging

Use debug/information-level logs for channel creation, join, leave, kick, and malformed command parsing. Avoid warning-level logs for unsupported client-side chat actions once they are deliberately consumed.

## Error Handling

Malformed `0xB3` actions should never bring down the session loop.

Rules:

- invalid packet shape: reject at packet parse layer
- unknown action id: consume and answer with a system message
- invalid target user/channel: consume and answer with a system message
- authorization failure: consume and answer with a system message

This keeps behavior deterministic and avoids console noise.

## Testing Strategy

Testing should be split into:

- packet tests
  - `0xB3` parse of language, action id, payload
  - `0xB2` serialization for added commands
- service tests
  - open window
  - create/join/leave channel
  - send message
  - PM delivery rules
  - ignore behavior
  - moderator/voice transitions
  - password and rename rules
  - kick
  - whois visibility
  - emote routing
- handler tests
  - `0xB5` delegates correctly
  - `0xB3` delegates correctly
- cleanup tests
  - disconnect/close removes membership
  - empty channel is removed

## Non-Goals

This design does not include:

- persistence of channels or chat history
- bulletin-board-style archival messaging
- guild or party chat replacement
- Lua-driven chat conference rules
- additional client UI beyond standard `0xB2` / `0xB3` / `0xB5`

## Recommended File Shape

Keep the implementation decomposed into focused files:

- packet parsing/writing
- handler
- service interface
- service implementation
- runtime state models
- command/action helpers

Do not put the entire chat system into one giant service file with one huge `switch`.
