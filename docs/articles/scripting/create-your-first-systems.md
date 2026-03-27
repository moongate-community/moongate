# Create Your First Systems

This page is the second beginner path after [Create Your First Content](create-your-first-content.md).

The first path teaches the basics:

- one item template
- one Lua brain
- one NPC template

This second path teaches small gameplay systems that combine those building blocks into something more useful.

These guides assume:

- your shard root is `~/moongate`
- the server already runs against `--root-directory ~/moongate`
- you can restart the server after changes
- you can log in with an account that can run in-game admin commands
- you already know the difference between `~/moongate/**` and the repository `moongate_data/**`

## Learning Path

Follow these in order:

1. [Create Your First Item Script](create-your-first-item-script.md)
2. [Create Your First Loot Container](create-your-first-loot-container.md)
3. [Create Your First Scheduled Event](create-your-first-scheduled-event.md)
4. [Create Your First Gump](create-your-first-gump.md)
5. [Create Your First Lua Admin Command](create-your-first-lua-admin-command.md)
6. [Create Your First Plugin](../architecture/create-your-first-plugin.md)
7. [Create Your First C# Admin Command](../architecture/create-your-first-csharp-admin-command.md)

That order keeps the beginner path clean:

- item script teaches item behavior
- loot container teaches template-driven item generation
- scheduled event teaches shard-level timed logic
- gump teaches player-facing UI on top of the item script path
- Lua admin command teaches scripted operator workflows
- plugin and C# command move into compiled extensions only after the Lua path is clear

## Shard Systems

These guides stay inside the shard root:

- `~/moongate/templates/**`
- `~/moongate/scripts/**`

They are the fastest way to add gameplay and operator tools without compiling C#.

## Extension Path

The last two guides move into compiled extensions:

- [Create Your First Plugin](../architecture/create-your-first-plugin.md)
- [Create Your First C# Admin Command](../architecture/create-your-first-csharp-admin-command.md)

Use that path when you need:

- a real compiled console command
- plugin-local services or handlers
- something you do not want to keep in Lua

## What You Will Use

Across the phase 2 guides you will only see commands and paths that already exist in the repo:

- `moongate-template validate --root-directory ~/moongate`
- `.spawn_item <templateId>`
- `dotnet new moongate-plugin ...`
- `dotnet build`
- `bash scripts/pack-plugin.sh`

## After These Tutorials

Once you finish this path, the deeper references are here:

- [Lua Scripting Overview](overview.md)
- [Scripting API Reference](api.md)
- [Script Modules](modules.md)
- [Loot Containers](loot-containers.md)
- [Scheduled Events](scheduled-events.md)
- [Gump Tutorial](gump-tutorial.md)
- [Plugin System](../architecture/plugins.md)
- [Console Commands](../operations/console-commands.md)
