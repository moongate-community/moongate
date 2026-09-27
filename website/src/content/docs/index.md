---
title: Moongate documentation
description: Build, run, and extend the Moongate Ultima Online server and its reusable .NET libraries.
template: splash
hero:
  title: Moongate
  tagline: An open-source Ultima Online server, built for the joy of programming.
  image:
    file: ../../../../images/moongate_logo.png
  actions:
    - text: Start here
      link: /start/getting-started/
      icon: right-arrow
    - text: Explore documentation
      link: /#find-your-starting-point
      variant: secondary
---

Moongate is a personal project for revisiting Ultima Online, exploring ideas and
enjoying the work of building a server. After 25 years of programming, there is
still plenty to learn—and plenty worth writing by hand.

The foundation is C# and .NET 10, with Lua scripting, PostgreSQL persistence and
Redis-backed realm handoff. Its libraries can also be used on their own.

> **A work in progress.** There is no playable world yet.
> [See what works today](/start/implementation-status/) before setting up a server.

## Find your starting point

<nav class="docs-paths" aria-label="Documentation paths">
  <a href="/start/getting-started/">
    <strong>Run the server</strong>
    <span>Prepare a server root, configure its dependencies and make your first connection.</span>
  </a>
  <a href="/server/scripting/">
    <strong>Script and create content</strong>
    <span>Write Lua scripts and define the shard with data files and TOML templates.</span>
  </a>
  <a href="/server/plugins/">
    <strong>Extend with C#</strong>
    <span>Add plugins, commands and Lua modules using the server's extension points.</span>
  </a>
  <a href="/reference/nuget-packaging/">
    <strong>Use the libraries</strong>
    <span>Bring networking, persistence and other Moongate libraries into your own projects.</span>
  </a>
</nav>

Looking for a reference? Browse [data files](/server/data-files/),
[server configuration](/server/configuration/), [Docker](/server/docker/),
or [migrating from UOX3](/server/uox3-migration/).

## Install on Linux

<span id="install-in-one-line"></span>

```sh
curl -fsSL https://moongate.sh/install.sh | sh
```

The installer downloads the latest release for Linux x64 or ARM64 into
`/opt/moongate`. Keep your server root outside that directory, because upgrades
replace the installation. Follow [First start](/start/getting-started/) to prepare
the client files, databases and configuration before running the server.

[Read the installation guide](/start/install/) for the script, installation options,
upgrades and removal.

## Built to learn

The process matters as much as the result. [How I use AI](/start/ai-usage/) explains
where it helps with migration, tests and design, and why writing code by hand
remains part of the project.

To take part, start with the [contribution guide](/contributing/getting-started/).
Code, tests, examples and [documentation](/contributing/documentation/) are all
ways to contribute.

## Releases

The site header identifies the documented version. A publication made by hand between
releases keeps the label of the latest release, so a page may describe work that is not
in it yet. Read the
[changelog](/start/changelog/) for features, fixes, and breaking changes.
