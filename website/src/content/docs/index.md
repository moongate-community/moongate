---
title: Moongate documentation
description: Build, run, and extend the Moongate Ultima Online server and its reusable .NET libraries.
template: splash
hero:
  title: Moongate documentation
  tagline: An Ultima Online server, with reusable .NET libraries for networking, persistence, scripting, and internal APIs.
  image:
    file: ../../../../images/moongate_logo.png
  actions:
    - text: Start here
      link: /start/getting-started/
      icon: right-arrow
    - text: Internal API guide
      link: /libraries/api/
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

- **Run:** [Install on Linux](/start/install/), [first start from source](/start/getting-started/), [Docker](/server/docker/) and [configuration](/server/configuration/).
- **Keep data:** [entities and data access](/server/persistence/), [migrations](/server/persistence-migrations/) and [operating PostgreSQL](/server/persistence-operations/).
- **Build game behavior:** [Packets and handlers](/server/packets/), [game loop and timers](/server/game-loop-and-timers/), [Lua scripts](/server/scripting/).
- **Extend or reuse:** [Plugins](/server/plugins/), [standalone TCP](/libraries/network-cookbook/) and [NuGet libraries](/reference/nuget-packaging/).
- **Contribute:** [Contribution guide](/contributing/getting-started/) and [writing documentation](/contributing/documentation/).

[Implementation status](/start/implementation-status/) says what the server does
today and what it does not.

## Releases

The site header identifies the documented version. A publication made by hand between
releases keeps the label of the latest release, so a page may describe work that is not
in it yet. Read the
[changelog](/start/changelog/) for features, fixes, and breaking changes.
