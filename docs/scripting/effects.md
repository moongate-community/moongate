# Effects

This page is part of [Writing Lua scripts](../scripting.md). The functions are listed in the reference:
[`effect`](https://moongate.sh/lua/effect/).

The `effect` module shows graphic effects to the players of the map within the view range
(`ultima.world.view_range`); a moving effect also reaches those in range of where it arrives.
The graphic is an art id: `EffectGraphicType` names the animations the emulators use
(`Smoke`, `LargeFireball`, `SmallFireball`, `FireColumn`, `Explosion`, `SparkleHeal`,
`SparkleBless`, `SparkleCurse`, `Fizzle`, `SmallBolt`, `Glow`, the four fields and others; the
generated `definitions.lua` lists them all), and any other art id works too.

`effect.at`, `effect.on` and `effect.moving` take an optional table of options:

| Option | Meaning |
| --- | --- |
| `speed`, `duration` | 0 to 255 each; both default to 10 |
| `hue` | Hue of the graphic, 0 to 65535 |
| `render` | An `EffectRenderModeType`: `Normal`, `Darken`, `Lighten`, `LightenTransparent`, `Translucent`, `TranslucentColor`, `Negative`, `NegativeTransparent` |
| `fixed_direction`, `explodes` | For a moving effect: keep the graphic's direction, and explode on arrival |
| `particle`, `explode_particle`, `explode_sound` | Particle effect ids and the arrival sound; only the Enhanced Client shows particles |
| `layer` | An `EffectLayerType`, the body part the particles are shown at: `Head`, `RightHand`, `LeftHand`, `Waist`, `LeftFoot`, `RightFoot`, `CenterFeet`, or `None`, the default |

```lua
-- A fireball from the caster to the target, exploding there.
effect.moving(caster, target, EffectGraphicType.LargeFireball, { speed = 7, duration = 0, explodes = true })

-- The healing sparkle on a mobile; the Enhanced Client also gets particles at the waist.
effect.on(who, EffectGraphicType.SparkleHeal, { speed = 9, duration = 32, particle = 5005, layer = EffectLayerType.Waist })
```

A function returns `false` and plays nothing for a value out of range, an option of the wrong
type (`speed = "9"`, `explodes = 1`), an option it does not know, and an effect with neither a
graphic nor a particle. An argument of the wrong type raises an error, as for every module. A classic client draws no particles: it gets the graphic, and
nothing for an effect made of particles only; a moving effect with the graphic `1`, ModernUO's
placeholder, counts as one. A lightning bolt, `effect.lightning(serial, hue = 0)`, has no graphic and is always sent. Effects are not sequenced: chain them with
`timer` calls.

`effect.at` plays an effect graphic that stays at a point of a map, such as the smoke of a teleport:

```lua
effect.at(MapType.Trammel, 1600, 1628, 5, EffectGraphicType.Smoke)
```
