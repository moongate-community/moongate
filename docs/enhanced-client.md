# Enhanced Client

Moongate accepts the Enhanced Client (EC) next to the classic client. Support is partial: this
page says how to connect one, what was seen working with a real client and what was not
tried yet. The last test used client 4.0.117 (wire version `67.0.117.0`) on 2026-10-02.

## Connect an Enhanced Client

The EC always encrypts its connection, so the server must accept encrypted logins. Set the
encryption policy and the client's wire version in `config/moongate.toml`:

```toml
[network.encryption]
mode = "Optional"
client_version = "67.0.117.0"
```

- `Optional` accepts the encrypted EC and plaintext classic clients on the same listeners.
  `Required` accepts only encrypted clients. There is no automatic mode.
- `client_version` is the **wire version**: the version of the client executable with 60 added
  to the first number. An executable with file version `4.0.117.x` is `67.0.117.0`. The last
  number does not change the keys.
- The server uses one version for decryption. An encrypted classic client of another version
  is refused while this profile is set.

Restart the server after the change. The startup log names the profile:

```text
Client encryption: Optional; client 67.0.117.0; login XOR; game Twofish / MD5-XOR
```

and a login from the EC reads:

```text
Login client connected with version 67.0.117.0 (Enhanced)
```

See [UO client encryption](server-configuration.md#uo-client-encryption) for every mode and
the validation rules.

## What works

Seen with a real client:

| Step | Notes |
| --- | --- |
| Encrypted login and server list | The login server accepts the hardware information (`0xD9`) the EC sends right after the account login. |
| Character creation | `0x8D`. The face and shirt style the EC sends are not kept, and it has no pants colour choice. |
| Entering the world | A created character enters at once, as after choosing one from the list. |
| Walking | |

Built for the EC and covered by automated tests, but not yet seen with a real client:

| Feature | Notes |
| --- | --- |
| Speech | `0xAD` is read leniently: an odd language code, a missing terminator or a badly encoded character no longer disconnect the client. |
| Containers | The EC shows a container as a grid. Every item has its own slot (0 to 124), kept in the database; a drop takes the slot the client asks for, or the next free one. See [Packets](packets.md). |
| Profile request | `0xB8`, sent by the EC after entering the world, is accepted and ignored: no profile is shown. |

## What is missing

- Everything the classic client lacks too: see the [feature checklist](feature-checklist.md).
- The character profile window.
- The old Kingdom Reborn AES/E3 negotiation.
- Anything not listed above has not been tried with the EC.

## When the client is disconnected

The EC sends packets the classic client never sends, and some known packets in another
shape. The server closes a connection that sends something it does not accept, and the log
says what it was:

```text
Rejected packet from session 2, opcode 0xAD, name UnicodeSpeechRequestPacket, 20 bytes: AD0014...
Opcode 0xB8 is not registered as an incoming packet; 11 bytes buffered after opcode.
Rejected login packet from session 1, opcode 0xD9
```

- `Rejected packet` on the game server: the packet is known but its content was refused. The
  line shows the first 64 bytes.
- `is not registered as an incoming packet`: the opcode is unknown to the server.
- `Rejected login packet`: the login server has no handler for that opcode.

Open an issue with the line: the bytes are what is needed to add the packet.
