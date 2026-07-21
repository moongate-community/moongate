# Notifications

Moongate sends messages to people — account verification today, more later —
through a channel-agnostic pipeline. What a notification *says* lives in a
template on disk; what carries it is a **channel**.

## Where templates live

```
<root>/notification/templates/
  log/
    account_verification.mgtmpl
  email/
    account_verification.mgtmpl
```

Two conventions, and there is no registry to maintain:

- **The directory name is the channel id.** Adding a channel means adding a
  directory.
- **The file name, without its extension, is the template id.**

Template ids are `snake_case` and contain no dots. The defaults are seeded into
the runtime root on first boot; edit them there, not in the repository.

> [!NOTE]
> `.mgtmpl` files are [Scriban](https://github.com/scriban/scriban) templates.
> The extension is Moongate's own, so map `*.mgtmpl` to Scriban — or to Liquid,
> which is close enough — in your editor to get highlighting.

## Writing a template

The body uses `{{ }}`. Model members are exposed in **snake_case**: a model
carrying `Username` is written `{{ username }}`.

A subject — for channels that have one — goes in Scriban **front matter**,
between `+++` markers. Front matter is already in script mode, so it takes no
braces:

```
+++
subject = "Verify your " + shard_name + " account"
+++
Hello {{ username }},

Confirm your account:
{{ website }}/verify?token={{ token }}
```

Channels without a subject simply omit the front matter block.

Note that the verification URL is composed **here**, not in the server: its
shape belongs to your website, so changing it costs an edit rather than a
release.

## Available templates

| Template | Raised when | Model |
|---|---|---|
| `account_verification` | A web registration creates a pending account | `username`, `email`, `token`, `website`, `shard_name` |

`website` comes from the shard's **Website** contact in the server settings; if
it is unset the value is empty, and any link built from it will be broken.

## Channels

`log` is the only channel shipped today: it writes the rendered notification to
the server log, which is how the verification token is reachable on a shard with
no transport configured. The `email/` templates ship ready for the SMTP channel
that follows; addressing a channel that is not registered logs a warning and
drops the notification.

A plugin adds a transport by implementing `INotificationChannel` and registering
it from its `Configure`:

```csharp
container.RegisterNotificationChannel<DiscordNotificationChannel>();
```

The channel's `Id` is the only coordination point: notifications addressed to
that id reach it, and it reads its templates from the directory of the same
name.

## Configuration

The `notifications` section of `moongate.yaml`:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `MaxAttempts` | int | `3` | Total delivery attempts per notification. |
| `RetryDelaySeconds` | int | `5` | Wait between one attempt and the next. |

> [!WARNING]
> **Delivery is best-effort.** Notifications are delivered on a worker thread and
> are not persisted: if every attempt fails, the failure is logged and the
> notification is gone. A lost verification message means an account that never
> activates — watch the log for `Gave up delivering`.

## Troubleshooting

A broken template does not stop the shard from booting. It is logged at startup
as `Skipping notification template <path>` and counted in the
`Loaded N notification template(s) ... (M skipped)` line. Fix the file and
restart.
