# Server guide

Moongate is a .NET 10 server executable. The current repository contains a framed TCP listener and handlers for the login, server-selection, game-login, and character-creation flow. It also integrates account and world persistence, Lua scripting, startup data loaders, and readers for Ultima client assets such as maps, art, animations, sounds, fonts, and localization data.

Those implemented components describe the repository's present scope; they are not a claim of complete protocol or client compatibility.

## Onboarding route

1. [Install the toolchain and build the server](./installation.md).
2. [Review paths and network settings](./configuration.md).
3. [Perform a first source launch](./first-launch.md).

After onboarding, use [Operations](./operations.md) and [Troubleshooting](./troubleshooting.md) as needed.
