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

## Find your starting point

- **Run:** [First start](/start/getting-started/), [Docker](/server/docker/) and [configuration](/server/configuration/).
- **Keep data:** [Persistence, world saves and recovery](/server/persistence/).
- **Build game behavior:** [Packets and handlers](/server/packets/), [game loop and timers](/server/game-loop-and-timers/), [Lua scripts](/server/scripting/).
- **Extend or reuse:** [Plugins](/server/plugins/), [standalone TCP](/libraries/network-cookbook/) and [NuGet libraries](/reference/nuget-packaging/).
- **Contribute:** [Contribution guide](/contributing/getting-started/) and [writing documentation](/contributing/documentation/).

The internal API guide covers the implemented transport and request/reply contracts.
Login-to-realm coordination is planned separately.

## Releases

The site header identifies the documented version. A publication made by hand between
releases keeps the label of the latest release, so a page may describe work that is not
in it yet. Read the
[changelog](/start/changelog/) for features, fixes, and breaking changes.
