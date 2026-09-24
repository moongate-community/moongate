---
title: Moongate documentation
description: Build, run, and extend the Moongate Ultima Online server and its reusable .NET libraries.
template: splash
hero:
  title: Moongate documentation
  tagline: An Ultima Online server with reusable .NET libraries for networking, persistence and scripting, plus Redis-backed realm handoff.
  image:
    file: ../../../../images/moongate_logo.png
  actions:
    - text: Start here
      link: /start/getting-started/
      icon: right-arrow
    - text: Docker login and realms
      link: /server/docker-login-realms/
      variant: secondary
---

## Install in one line

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

Installs the latest release on Linux, x64 and arm64: the archive's contents land in
`/opt/moongate` and the command becomes `moongate`. Start it with a root directory of its own,
`moongate --root-directory /srv/moongate`, because upgrades replace the installation directory.
[What it installs, how to upgrade, how to remove it](/start/install/), and how to read the
script before running it.

## Find your starting point

- **Run a shard:** [Install on Linux](/start/install/), [first start](/start/getting-started/), [Docker](/server/docker/), [configuration](/server/configuration/) and [operating PostgreSQL](/server/persistence-operations/).
- **Scripting and content:** [Lua scripts](/server/scripting/), [TOML templates](/server/templates/) and [migrating from UOX3](/server/uox3-migration/).
- **Extend with C#:** [plugins](/server/plugins/), [entities and data access](/server/persistence/), [migrations](/server/persistence-migrations/), [packets and handlers](/server/packets/) and [game loop and timers](/server/game-loop-and-timers/).
- **Libraries:** [NuGet packages](/reference/nuget-packaging/) and the [standalone TCP cookbook](/libraries/network-cookbook/).
- **Contribute:** [Contribution guide](/contributing/getting-started/) and [writing documentation](/contributing/documentation/).

[Implementation status](/start/implementation-status/) says what the server does
today and what it does not.

## Releases

The site header identifies the documented version. A publication made by hand between
releases keeps the label of the latest release, so a page may describe work that is not
in it yet. Read the
[changelog](/start/changelog/) for features, fixes, and breaking changes.
