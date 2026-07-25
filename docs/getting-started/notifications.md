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
{{ verification_url }}
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

Moongate builds `verification_url` from the persisted `Contacts.Website` portal
base. It preserves the base path, removes trailing `/` characters, appends
`/verify`, replaces any existing query with the escaped `token` query parameter,
and removes any fragment. For example, a base ending in
`/portal/?old=true#section` produces `/portal/verify?token=...`.

## Available templates

| Template | Raised when | Model |
|---|---|---|
| `account_verification` | A web registration creates a pending account or rotates its token on resend | `username`, `email`, `token`, `website`, `verification_url`, `shard_name` |

All account-verification fields remain available to custom templates:

| Field | Meaning |
|---|---|
| `username` | Pending account username. |
| `email` | Destination email address. |
| `token` | Raw single-use token, retained for legacy custom templates. Prefer `verification_url` for links. |
| `website` | Unmodified `Contacts.Website` portal base, retained for legacy custom templates. |
| `verification_url` | Canonical, escaped verification URL built by Moongate. |
| `shard_name` | Shard name from `moongate.ShardName`. |

Public registration is not ready unless `website` is an absolute HTTP(S) URL
with a host, so newly queued registration messages always receive a usable
`verification_url`.

## Channels

`log` writes the rendered notification to the server log. It always works and
remains useful for notification development, but public registration readiness
requires account verification to be routed to `email`.

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
`<root>/plugins/configs/smtp.yaml`. The keys below belong under the file's
top-level `smtp` section:

| Key | Type | Default | Meaning |
|---|---|---|---|
| `Host` | string | `''` | SMTP host. **Empty leaves the channel unregistered.** |
| `Port` | int | `587` | Submission port. |
| `Security` | enum | `Auto` | `None`, `StartTls`, `SslOnConnect`, or `Auto`. See the warning below before relying on `Auto`. |
| `Username` | string | `''` | Empty means no authentication, as a local relay usually wants. |
| `Password` | string | `''` | See the note on secrets below. |
| `FromAddress` | string | `''` | Sender. **Empty leaves the channel unregistered.** |
| `FromName` | string | `''` | Display name; falls back to the shard name. |
| `TimeoutSeconds` | int | `30` | Connect and send timeout. |

> [!IMPORTANT]
> **Configuring SMTP is not enough to send verification mail.** Point
> `notifications.AccountVerificationChannel` in `moongate.yaml` at `email` as
> well, then restart. The server states which channel it will use at startup,
> and warns when that channel is not registered — check the log if nothing
> arrives.

> [!WARNING]
> **`Auto` does not guarantee encryption.** It selects implicit TLS only on port 465; on every other
> port it uses STARTTLS *when the server advertises it* and otherwise continues in plaintext. Use
> `StartTls` on 587 or `SslOnConnect` on 465 whenever you configure a `Username`.
>
> As a backstop the plugin refuses to authenticate on a connection that never became encrypted,
> logging `SMTP connection is not encrypted; not retrying` and dropping the message rather than
> putting your password on the wire. A relay with no credentials is unaffected.

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

## Registration readiness and delivery

The registration readiness reported by the HTTP API is deliberately local. It
means that:

- `Contacts.Website` is an absolute HTTP(S) URL with a host;
- `notifications.AccountVerificationChannel` selects `email`; and
- the SMTP plugin registered the `email` channel because `Host` and
  `FromAddress` were present when Moongate started.

Readiness does **not** connect to the SMTP server, validate credentials, prove
that the destination accepts mail, or guarantee durable delivery. It therefore
does not replace startup-log checks or delivery monitoring.

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
