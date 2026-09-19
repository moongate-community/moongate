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
      link: /moongate/start/getting-started/
      icon: right-arrow
    - text: Internal API guide
      link: /moongate/libraries/api/
      variant: secondary
---

## Find your starting point

- **Run:** [First start](/moongate/start/getting-started/), [Docker](/moongate/server/docker/) and [configuration](/moongate/server/configuration/).
- **Keep data:** [Persistence, world saves and recovery](/moongate/server/persistence/).
- **Build game behavior:** [Packets and handlers](/moongate/server/packets/), [game loop and timers](/moongate/server/game-loop-and-timers/), [Lua scripts](/moongate/server/scripting/).
- **Extend or reuse:** [Plugins](/moongate/server/plugins/), [standalone TCP](/moongate/libraries/network-cookbook/) and [NuGet libraries](/moongate/reference/nuget-packaging/).
- **Contribute:** [Contribution guide](/moongate/contributing/getting-started/) and [writing documentation](/moongate/contributing/documentation/).

The internal API guide covers the implemented transport and request/reply contracts.
Login-to-realm coordination is planned separately.

## Releases

The site header identifies the documented version. A docs-only development refresh
is labelled separately from the latest release. Read the
[changelog](/moongate/start/changelog/) for features, fixes, and breaking changes.
