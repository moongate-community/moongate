## at

The smoke of a teleport, at a point of a map:

```lua
effect.at(MapType.Trammel, 1600, 1628, 5, EffectGraphicType.Smoke)
```

## moving

A fireball from the caster to the target, exploding there:

```lua
effect.moving(caster, target, EffectGraphicType.LargeFireball, { speed = 7, duration = 0, explodes = true })
```

## on

The healing sparkle on a mobile; the Enhanced Client also gets particles at the waist:

```lua
effect.on(who, EffectGraphicType.SparkleHeal, { speed = 9, duration = 32, particle = 5005, layer = EffectLayerType.Waist })
```
