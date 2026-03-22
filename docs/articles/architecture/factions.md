# Factions

Moongate v2 `Factions v1` adds a JSON-loaded faction registry plus runtime faction membership on mobiles.

## Scope

This first iteration is intentionally narrow:

- load faction definitions from `moongate_data/templates/factions/*.json`
- persist a mobile `FactionId` on `UOMobileEntity`
- support `defaultFactionId` on NPC mobile templates
- resolve faction hostility inside `NotorietyService` and `AiRelationService`
- use faction hostility in Lua perception helpers

This iteration does not yet include:

- player join/leave flows
- faction ranks, silver, towns, elections, or stones
- faction-specific items or UI

## Data Model

Faction definitions live in JSON and currently describe:

- `id`
- `name`
- `description`
- `tags`
- `enemyFactionIds`

Example:

```json
{
  "type": "faction",
  "id": "true_britannians",
  "name": "True Britannians",
  "enemyFactionIds": ["shadowlords", "minax"]
}
```

The default dataset ships in:

- `moongate_data/templates/factions/modernuo.json`

## Mobile Templates

NPC templates can opt into a default faction through:

```json
{
  "type": "mobile",
  "id": "faction_guard",
  "defaultFactionId": "true_britannians"
}
```

`defaultFactionId` is inherited through `base_mobile` chains and copied into the spawned runtime mobile.

Player faction membership is not sourced from templates. It belongs to runtime and persisted mobile state.

## Runtime Behavior

`UOMobileEntity` now persists:

- `FactionId`

That runtime membership is used by:

- `NotorietyService`
- `AiRelationService`
- `PerceptionModule`

Current rules:

- same faction -> friendly for AI, innocent for notoriety
- declared enemy factions -> hostile for AI, enemy notoriety
- recent aggression still overrides and keeps targets attackable
- criminals and murderers still resolve as hostile

## Validation

Startup template validation now fails when:

- a mobile references a missing `defaultFactionId`
- a faction references a missing enemy faction id

This keeps faction JSON and mobile templates aligned.
