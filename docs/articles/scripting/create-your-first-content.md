# Create Your First Content

This page is the beginner starting point for Moongate content authoring.

It is written for someone who already has a running shard root, but does **not** yet understand how Moongate templates and
Lua scripts fit together.

These guides assume:

- your shard root is `~/moongate`
- the server is already configured to run with `--root-directory ~/moongate`
- you can log in to the game with an account that can run in-game admin commands
- you have the template validator installed as `moongate-template`

Important path rule:

- when you are authoring your shard, edit files under `~/moongate/templates/**` and `~/moongate/scripts/**`
- when you are reading examples inside this repository, the bundled defaults live under `moongate_data/**`

## Learning Path

Follow the guides in this order:

1. [Create Your First Item Template](create-your-first-item-template.md)
2. [Create Your First NPC Brain](create-your-first-npc-brain.md)
3. [Create Your First NPC Template](create-your-first-npc-template.md)

That order keeps the dependency chain simple:

- the item guide teaches template structure without Lua
- the brain guide teaches Lua behavior without mobile JSON yet
- the NPC guide connects the two worlds by binding `ai.brain` to the Lua table you just created

## What You Will Use

The hands-on guides use only commands and flows already documented and present in the repo:

- `moongate-template validate --root-directory ~/moongate`
- `.spawn_item <templateId>`
- `.add_npc <templateId>`

To keep the first pass deterministic, the guides tell you to restart the server after editing tutorial files instead of
leaning on hot reload or single-file reload commands.

## If You Only Care About One Topic

- Want to learn item templates only: start with [Create Your First Item Template](create-your-first-item-template.md)
- Want to learn NPC authoring: do both [Create Your First NPC Brain](create-your-first-npc-brain.md) and
  [Create Your First NPC Template](create-your-first-npc-template.md)

## After The Tutorials

Once you finish the hands-on path, the deeper reference material is here:

- [Create Your First Systems](create-your-first-systems.md)
- [Lua Scripting Overview](overview.md)
- [NPC Behaviors](npc-behaviors.md)
- [Scripting API Reference](api.md)
- [In-Game Item Admin Commands](../operations/in-game-item-admin-commands.md)
- [Template Validation](../operations/template-validation.md)
