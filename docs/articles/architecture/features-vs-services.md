# Features vs Services

Moongate should not treat every gameplay change as a standalone top-level subsystem.

This page defines when a new area should become a dedicated `Feature` and when it should remain a normal `Service`.

## Use a Feature When

Create a dedicated feature area only when the domain is large enough to own its own behavior and boundaries.

Typical signals:

- the domain has its own state or lifecycle
- the domain has more than one integration point
- the domain spans C# code plus `moongate_data` scripts or templates
- the domain needs dedicated documentation
- the domain is easier to reason about as a named gameplay subsystem

Examples that fit this model:

- plants and gardening
- harvesting
- reputation
- quests
- factions

## Use a Service When

Keep the code as a normal service when the responsibility is local and supports a wider domain instead of defining a new one.

Typical signals:

- the code has one narrow responsibility
- the code does not need its own lifecycle
- the code is an implementation detail of another subsystem
- the code does not justify its own docs section

Examples that fit this model:

- `BandageService`
- `CombatAccuracyResolver`
- `ItemFactoryService`
- `MobileModifierAggregationService`

## Naming

Prefer `Features` over `Engines`.

`ModernUO` uses `Engines` mostly as a broad organizational convention, not as a strict runtime contract. Moongate should be stricter and simpler.

Recommended naming:

- `Moongate.Server.Features.Plants`
- `Moongate.Server.Features.Harvesting`
- `Moongate.Server.Features.Reputation`

Avoid creating a generic catch-all `Engines` area for unrelated systems.

## Standard Shape for a Feature

When a domain is large enough to become a feature, keep its structure predictable.

Recommended shape:

- `Interfaces/`
- `Services/`
- `Data/`
- `Types/`
- `Internal/`

Each feature should also have one clear entrypoint or orchestration layer, for example:

- `PlantSystemService`
- `HarvestSystemService`
- `ReputationSystemService`

## Decision Rule

Before creating a new top-level feature area, ask these questions:

1. Does this domain have state or lifecycle of its own?
2. Does it integrate with more than one boundary?
3. Would a dedicated docs page make sense for it?
4. Would grouping it as a named subsystem make the code easier to understand?

If the answer is mostly `no`, keep it as a service inside the existing domain.

If the answer is mostly `yes`, a feature boundary is justified.

## KISS Rule

Do not promote code into a feature too early.

Most gameplay additions should start as focused services. A feature boundary is justified only when the domain is clearly broader than one service and needs a stable, documented shape.

This keeps Moongate easier to navigate than the looser `Engines` model used by older UO server codebases.

---

**Previous**: [Architecture Overview](overview.md) | **Next**: [Network System](network.md)
