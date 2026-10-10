# account

Creates an account, or gives an administrator access to the administration API.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `account create <username> <password> [level]` / `account api-access <username> <on\|off>` | Yes | Yes | Administrator | Login |

```text
account create <username> <password> [Regular|GameMaster|Administrator]
```

The account level defaults to `Regular`. The command waits for
`IAccountService.CreateAccountAsync` and reports success, an existing username, or
an error without printing the password. The interactive console masks the password
token while it is typed and does not include the raw command line in its error log.
The account is stored in the shared Accounts PostgreSQL database.
Game-only processes do not register this command or receive Accounts credentials.

In-game administrators can type `.account create ...`. The server does not echo
or broadcast the input and does not write it to its logs. The UO client may
retain the typed command in its own local history.

Plugins can add commands through `RegisterCommand<TExecutor>`; see
[Writing a plugin](../plugins.md#console-commands).

Every text a command shows to players, its description in `help` and the dispatcher's replies
(unknown command, not available here, not allowed, failed) come from the message files
in the server language (`ILocalizationService`, ids 30008–30229; see
[Localization](../localization.md#moongates-own-messages)). Command syntax, account
types, sources and map names stay technical names, as the commands take them. On a
login-only process, which has no message files, the texts are English. Operator-only
console output (`account api-access`, `script`) stays English.

### Local API access provisioning

```text
account api-access <username> <on|off>
```

Available only in the Login/Standalone local console, even when the caller is an in-game Administrator. Accounts created with `account create` start with API access disabled. Enable an existing Administrator to provision the first panel user; disabling access revokes its administrative sessions across hosts. Game login is unaffected. See [Administration API](../admin-api.md).

## See also

- [All commands](../commands.md)
