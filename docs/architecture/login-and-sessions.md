# Login and sessions

The Ultima protocol path represented by the framers uses a login connection followed by a game-server reconnect with a raw seed. A short-lived auth key is intended to correlate the authenticated login session with that reconnect. Current handler enforcement is looser: `GameServerLoginHandler` validates the auth key but does not require a new connection, a raw seed, or a particular session state.

## Implemented flow

```text
login connection
  LoginSeedPacket (0xEF)
    -> record seed and client version
  AccountLoginRequestPacket (0x80)
    -> authenticate account
    -> denial (0x82), or mark session authenticated and send server list (0xA8)
  SelectServerPacket (0xA0)
    -> create single-use pending-login key
    -> send game-server redirect (0x8C)

intended game-server reconnect
  raw four-byte seed
    -> record seed and advance session to login phase
  GameServerLoginPacket (0x91)
    -> consume pending key
    -> restore username and resolve account id
    -> enable compression
    -> send support features (0xB9) and character list (0xA9)
  CharacterCreationPacket (0xF8)
    -> create and persist mobile
    -> link mobile to account when the account exists
    -> publish CharacterCreatedEvent
    -> attach mobile to session
```

The opcode values above come from the corresponding packet declarations. They are included to connect the flow to packet traces, not as a complete protocol table.

The diagram shows the intended wire sequence. The integration test covers seed, account login, redirect, auth-key game login, compression, and a returned character-list response, but it sends the game-login packet on the original socket without reconnecting or sending a raw seed. It therefore does not prove the two-connection transition end to end.

## Login connection

The declared login-seed packet moves an awaiting session into the login phase before `LoginSeedHandler` parses it. The handler stores the packet seed and client version. If a local client version can be read and differs from the remote version, it disconnects; if no local version is available, it does not enforce a comparison.

`AccountLoginHandler` delegates credentials to `IAccountService`. The current service queries persisted accounts, verifies the password hash, rejects missing credentials as bad credentials, and rejects inactive accounts as blocked. Success stores the username, changes the session state to authenticated, and returns a one-shard server list.

`SelectServerHandler` creates a `PendingLogin` from the session username and returns the configured public address, configured game port, and generated auth key. The current handler does not inspect the selected shard index. `PendingLoginStore` derives keys from an incrementing 32-bit counter and skips zero; keys are monotonic and non-zero over the practical pre-wrap range. It does not check for a live-key collision, and dictionary assignment can overwrite an entry after counter wrap. Lookup removes an entry on the first attempt and rejects it if expired. `Program.cs` configures a 30-second lifetime.

## Game connection and character list

In the intended client flow, the redirect leads to a new connection and therefore a new `PlayerSession`; its raw seed is framed and consumed separately before the game-server login packet. The server does not enforce that topology in `GameServerLoginHandler`: a valid pending key can be consumed from the original authenticated session as well. The handler has no session-state check. If it cannot take the key, it sends a communication-problem denial. The key is the implemented handoff credential—the account and password fields carried by `GameServerLoginPacket` are parsed but are not re-authenticated by this handler.

After a successful take, the handler restores the pending username, resolves its persisted account id, and stores it on `context.Session`. A missing account is logged and raises an exception, which the network dispatch boundary logs and contains. It then obtains the account's character names, enables outbound compression, and sends modern feature flags followed by seven character slots and the registered starting cities.

## Character creation boundary

`CharacterCreationHandler` passes the parsed creation request and session account id to `ICharacterService`. `CharacterService` asks the mobile factory to build the player mobile, persists it so a serial is assigned, links that serial into the account when found, publishes `CharacterCreatedEvent`, and returns the mobile. The handler attaches it to the current session.

This is the end of the implemented flow documented here. Character creation does not itself enter the character into the game world, and this handler sends no response packet.

## Source map

### Runtime

- `src/Moongate.Server/Handlers/LoginSeedHandler.cs`
- `src/Moongate.Server/Handlers/AccountLoginHandler.cs`
- `src/Moongate.Server/Handlers/SelectServerHandler.cs`
- `src/Moongate.Server/Handlers/GameServerLoginHandler.cs`
- `src/Moongate.Server/Handlers/CharacterCreationHandler.cs`
- `src/Moongate.Server/Interfaces/Accounts/IAccountService.cs`
- `src/Moongate.Server/Interfaces/Accounts/ICharacterService.cs`
- `src/Moongate.Server/Interfaces/Accounts/IPendingLoginStore.cs`
- `src/Moongate.Server/Interfaces/Accounts/ISessionManager.cs`
- `src/Moongate.Server/Services/Accounts/AccountService.cs`
- `src/Moongate.Server/Services/Accounts/CharacterService.cs`
- `src/Moongate.Server/Services/Accounts/PendingLoginStore.cs`
- `src/Moongate.Server/Services/Accounts/SessionManager.cs`
- `src/Moongate.Server/Data/Session/PlayerSession.cs`
- `src/Moongate.Network/Packets/Incoming/LoginSeedPacket.cs`
- `src/Moongate.Network/Packets/Incoming/AccountLoginRequestPacket.cs`
- `src/Moongate.Network/Packets/Incoming/SelectServerPacket.cs`
- `src/Moongate.Network/Packets/Incoming/GameServerLoginPacket.cs`
- `src/Moongate.Network/Packets/Incoming/CharacterCreationPacket.cs`
- `src/Moongate.Network/Packets/Outgoing/LoginDeniedPacket.cs`
- `src/Moongate.Network/Packets/Outgoing/ServerListPacket.cs`
- `src/Moongate.Network/Packets/Outgoing/ConnectToGameServerPacket.cs`
- `src/Moongate.Network/Packets/Outgoing/SupportFeaturesPacket.cs`
- `src/Moongate.Network/Packets/Outgoing/CharacterListPacket.cs`

### Tests

- `tests/Moongate.Tests/Server/LoginFlowIntegrationTests.cs`
- `tests/Moongate.Tests/Server/CharacterServiceTests.cs`
- `tests/Moongate.Tests/Server/PendingLoginStoreTests.cs`
- `tests/Moongate.Tests/Server/SeedHandshakeTests.cs`
- `tests/Moongate.Tests/Network/LoginFlowPacketsTests.cs`
- `tests/Moongate.Tests/Network/Packets/Incoming/GameServerLoginPacketTests.cs`
- `tests/Moongate.Tests/Network/Packets/Incoming/CharacterCreationPacketTests.cs`
- `tests/Moongate.Tests/Network/Packets/Outgoing/CharacterListPacketTests.cs`
