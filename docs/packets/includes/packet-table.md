<div class="mg-stats">
  <div class="mg-stat"><div class="mg-stat-num">45</div><div class="mg-stat-label">implemented packets</div></div>
  <div class="mg-stat"><div class="mg-stat-num mg-grass">14</div><div class="mg-stat-label">incoming (client → server)</div></div>
  <div class="mg-stat"><div class="mg-stat-num mg-violet">31</div><div class="mg-stat-label">outgoing (server → client)</div></div>
  <div class="mg-stat"><div class="mg-stat-num mg-stone">7.x</div><div class="mg-stat-label">client target</div></div>
</div>

| Opcode | Name | Dir | Size | Description |
|---|---|---|---|---|
| [`0x02`](../incoming/0x02-move-request.md) | Move Request | C → S | 7 bytes (fixed) | One step or turn, with anti-fastwalk key. |
| [`0x06`](../incoming/0x06-double-click.md) | Double Click | C → S | 5 bytes (fixed) | The client double-clicked an entity, identified by its serial. |
| [`0x09`](../incoming/0x09-single-click.md) | Single Click | C → S | 5 bytes (fixed) | The client clicked an entity, identified by its serial. |
| [`0x11`](../outgoing/0x11-status-bar-info.md) | Status Bar Info | S → C | Variable | The player's own status window. |
| [`0x1B`](../outgoing/0x1b-login-confirm.md) | Login Confirm | S → C | 37 bytes (fixed) | The first packet of the enter-world burst. |
| [`0x1D`](../outgoing/0x1d-delete-object.md) | Delete Object | S → C | 5 bytes (fixed) | The entity is gone — stop drawing it. |
| [`0x20`](../outgoing/0x20-draw-game-player.md) | Draw Game Player | S → C | 19 bytes (fixed) | Positions and renders the player's own mobile. |
| [`0x22`](../outgoing/0x22-movement-ack.md) | Movement Ack | S → C | 3 bytes (fixed) | Confirms the step with the client's sequence number. |
| [`0x24`](../outgoing/0x24-draw-container.md) | Draw Container | S → C | 7 bytes (fixed) | Opens the container's gump on the client. |
| [`0x25`](../outgoing/0x25-add-item-to-container.md) | Add Item To Container | S → C | 21 bytes (fixed) | Drops one item into an already-open container gump. |
| [`0x27`](../outgoing/0x27-lift-reject.md) | Lift Reject | S → C | 2 bytes (fixed) | The lift the client asked for is refused, and why. |
| [`0x2E`](../outgoing/0x2e-worn-item.md) | Worn Item | S → C | 15 bytes (fixed) | Draws a single item on a mobile that the client already knows about. |
| [`0x3A`](../incoming/0x3a-skill-lock-change.md) | Skill Lock Change | C → S | Variable | The client sets the up/down/lock arrow on one skill. |
| [`0x3A`](../outgoing/0x3a-skills.md) | Skills | S → C | Variable | Skill list (0x3A), in the absolute-with-caps form (type 0x02): the client's whole skill list in one go. |
| [`0x3C`](../outgoing/0x3c-container-content.md) | Container Content | S → C | Variable | Every item inside a container, in one variable-length packet. |
| [`0x4E`](../outgoing/0x4e-personal-light-level.md) | Personal Light Level | S → C | 6 bytes (fixed) | The light radiating around the given mobile. |
| [`0x4F`](../outgoing/0x4f-overall-light-level.md) | Overall Light Level | S → C | 2 bytes (fixed) | 0 is full daylight, higher is darker. |
| [`0x55`](../outgoing/0x55-login-complete.md) | Login Complete | S → C | 1 bytes (fixed) | The "you are now in the world" marker that unblocks the client. |
| [`0x5B`](../outgoing/0x5b-game-time.md) | Game Time | S → C | 4 bytes (fixed) | The in-world clock shown to the client. |
| [`0x5D`](../incoming/0x5d-character-select.md) | Character Select | C → S | 73 bytes (fixed) | The client picks an existing character slot to enter the world with. |
| [`0x72`](../outgoing/0x72-war-mode.md) | War Mode | S → C | 5 bytes (fixed) | Toggles the client's combat stance. |
| [`0x73`](../incoming/0x73-ping.md) | Ping | C → S | 2 bytes (fixed) | The client sends this periodically with a rolling sequence byte and expects the server to echo it straight back, or it eventually drops the connection. |
| [`0x73`](../outgoing/0x73-ping-ack.md) | Ping Ack | S → C | 2 bytes (fixed) | Echoes the client's keep-alive sequence byte straight back. |
| [`0x78`](../outgoing/0x78-draw-object.md) | Draw Object | S → C | Variable | Draws a mobile and its equipped items on the client. |
| [`0x80`](../incoming/0x80-account-login-request.md) | Account Login Request | C → S | 62 bytes (fixed) | Credentials for the login server. |
| [`0x82`](../outgoing/0x82-login-denied.md) | Login Denied | S → C | 2 bytes (fixed) | Rejects the login with a protocol reason code. |
| [`0x83`](../incoming/0x83-delete-character.md) | Delete Character | C → S | 39 bytes (fixed) | The client asks to delete the character in the given slot. |
| [`0x85`](../outgoing/0x85-character-delete-result.md) | Character Delete Result | S → C | 2 bytes (fixed) | Why a deletion was refused. |
| [`0x86`](../outgoing/0x86-character-list-update.md) | Character List Update | S → C | 304 bytes (fixed) | The account's character list after it changed, so the client can redraw the selection screen. |
| [`0x88`](../outgoing/0x88-paperdoll.md) | Paperdoll | S → C | 66 bytes (fixed) | Tells the client to open the character window for a mobile. |
| [`0x8C`](../outgoing/0x8c-connect-to-game-server.md) | Connect To Game Server | S → C | 11 bytes (fixed) | Redirects the client to the game port with an auth key. |
| [`0x91`](../incoming/0x91-game-server-login.md) | Game Server Login | C → S | 65 bytes (fixed) | The auth key from the redirect plus the account credentials. |
| [`0xA0`](../incoming/0xa0-select-server.md) | Select Server | C → S | 3 bytes (fixed) | The shard index the client picked from the server list. |
| [`0xA8`](../outgoing/0xa8-server-list.md) | Server List | S → C | Variable | Advertises the available shards. |
| [`0xA9`](../outgoing/0xa9-character-list.md) | Character List | S → C | Variable | The character slots followed by the starting cities, in the extended 7.0.13+ layout. |
| [`0xB9`](../outgoing/0xb9-support-features.md) | Support Features | S → C | 5 bytes (fixed) | Unlocks the client feature set at login, sent right before the character list. |
| [`0xBC`](../outgoing/0xbc-season-change.md) | Season Change | S → C | 3 bytes (fixed) | Sets the client's season and optionally plays the season-change sound. |
| [`0xBD`](../incoming/0xbd-client-version.md) | Client Version | C → S | Variable | The client answers the server's version request with its build string (e.g. |
| [`0xBF`](../incoming/0xbf-general-information.md) | General Information | C → S | Variable | A multiplexed request whose meaning is chosen by a leading `SubCommand` (ushort). |
| [`0xBF/0x08`](../outgoing/0xbf-map-change.md) | Map Change | S → C | 6 bytes (fixed) | Switches the client to the given map. |
| [`0xBF/0x18`](../outgoing/0xbf-map-patches.md) | Map Patches | S → C | 41 bytes (fixed) | Declares the static/land map-diff block counts for the four classic facets. |
| [`0xBF/0x19`](../outgoing/0xbf-stat-lock-info.md) | Stat Lock Info | S → C | 12 bytes (fixed) | The up/down/lock state of the three stats, packed two bits each into a single byte. |
| [`0xEF`](../incoming/0xef-login-seed.md) | Login Seed | C → S | 21 bytes (fixed) | Connection seed and client version, sent first by ClassicUO. |
| [`0xF3`](../outgoing/0xf3-world-item.md) | World Item | S → C | 24 bytes (fixed) | Draws an item lying in the world. |
| [`0xF8`](../incoming/0xf8-character-creation.md) | Character Creation | C → S | 106 bytes (fixed) | The new 106-byte creation packet sent by clients 7.0.16.0 and later. |
