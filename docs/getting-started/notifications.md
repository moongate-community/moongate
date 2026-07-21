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

Front matter also carries the **content type**. A template that renders HTML
says so, and anything else — including omitting the line — is plain text:

```
+++
subject = "Verify your " + shard_name + " account"
content_type = "html"
+++
<p>Hello {{ username }},</p>
```

The rule is deliberately forgiving: only the exact value `html` selects HTML, so
a mistyped `htlm` costs you a plain-text mail rather than a lost notification.
An HTML mail is sent as a single HTML part, not as HTML with a plain-text
alternative.

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

`log` writes the rendered notification to the server log. It always works, which
makes it the channel a shard falls back on before a real transport is set up,
and it is where the account verification token appears by default.

`email` is provided by the SMTP plugin, described below. Addressing a channel
that is not registered logs a warning and drops the notification.

A plugin adds a transport by implementing `INotificationChannel` and registering
it from its `Configure`:

```csharp
container.RegisterNotificationChannel<DiscordNotificationChannel>();
```

The channel's `Id` is the only coordination point: notifications addressed to
that id reach it, and it reads its templates from the directory of the same
name.

## Sending email

Email is delivered by `Moongate.Smtp.Plugin`, configured in
`<root>/plugins/configs/smtp.yaml`:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `Host` | string | `''` | SMTP host. **Empty leaves the channel unregistered.** |
| `Port` | int | `587` | Submission port. |
| `Security` | enum | `Auto` | `None`, `StartTls`, `SslOnConnect`, or `Auto` — implicit TLS on 465, STARTTLS elsewhere when offered. |
| `Username` | string | `''` | Empty means no authentication, as a local relay usually wants. |
| `Password` | string | `''` | See the note on secrets below. |
| `FromAddress` | string | `''` | Sender. **Empty leaves the channel unregistered.** |
| `FromName` | string | `''` | Display name; falls back to the shard name. |
| `TimeoutSeconds` | int | `30` | Connect and send timeout. |

> [!IMPORTANT]
> **Configuring SMTP is not enough to send verification mail.** Point
> `notifications.AccountVerificationChannel` in `moongate.yaml` at `email` as
> well. The server states which channel it will use at startup, and warns when
> that channel is not registered — check the log if nothing arrives.

### Secrets

`MOONGATE_SMTP_PASSWORD` overrides `Password` when it is set, which is how a
container deployment should supply it. When you do keep the password in the
file, `chmod 600` it and keep it out of version control.

### What is retried

The channel decides. A timeout, an unreachable host or a `4xx` reply is
transient, so it is retried up to `MaxAttempts`. An authentication failure or a
`5xx` reply is permanent: it is logged once and not retried, because the answer
will not change and repeatedly presenting bad credentials can trip a provider's
rate limits.

## Configuration

The `notifications` section of `moongate.yaml`:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `AccountVerificationChannel` | string | `log` | Which channel account verification is delivered on. |
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
