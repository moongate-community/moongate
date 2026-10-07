<!-- translation: {"sourceHash":"212898f14cc0186c45b720658c38c5bf565eaafc26ed57c988882d8fbfe79b6d","title":"Effetti"} -->

# Effetti

Questa pagina fa parte di [Scrivere script Lua](../scripting.md). Le funzioni sono
elencate nel riferimento: [`effect`](https://moongate.sh/lua/effect/).

Il modulo `effect` mostra effetti grafici ai giocatori della mappa entro il raggio
visivo (`ultima.world.view_range`); un effetto in movimento raggiunge anche quelli
vicini alla destinazione. La grafica è un art id: `EffectGraphicType` nomina le
animazioni usate dagli emulatori (`Smoke`, `LargeFireball`, `SmallFireball`,
`FireColumn`, `Explosion`, `SparkleHeal`, `SparkleBless`, `SparkleCurse`, `Fizzle`,
`SmallBolt`, `Glow`, i quattro campi e altre; il `definitions.lua` generato le
elenca tutte), e funziona anche qualsiasi altro art id.

`effect.at`, `effect.on` ed `effect.moving` accettano una tabella facoltativa di opzioni:

| Opzione | Significato |
| --- | --- |
| `speed`, `duration` | Da 0 a 255 ciascuno; entrambi predefiniti a 10 |
| `hue` | Tonalità della grafica, da 0 a 65535 |
| `render` | Un `EffectRenderModeType`: `Normal`, `Darken`, `Lighten`, `LightenTransparent`, `Translucent`, `TranslucentColor`, `Negative`, `NegativeTransparent` |
| `fixed_direction`, `explodes` | Per un effetto in movimento: mantenere la direzione della grafica ed esplodere all'arrivo |
| `particle`, `explode_particle`, `explode_sound` | Id degli effetti particellari e suono di arrivo; solo Enhanced Client mostra le particelle |
| `layer` | Un `EffectLayerType`, la parte del corpo dove vengono mostrate le particelle: `Head`, `RightHand`, `LeftHand`, `Waist`, `LeftFoot`, `RightFoot`, `CenterFeet`, o `None`, il valore predefinito |

```lua
-- A fireball from the caster to the target, exploding there.
effect.moving(caster, target, EffectGraphicType.LargeFireball, { speed = 7, duration = 0, explodes = true })

-- The healing sparkle on a mobile; the Enhanced Client also gets particles at the waist.
effect.on(who, EffectGraphicType.SparkleHeal, { speed = 9, duration = 32, particle = 5005, layer = EffectLayerType.Waist })
```

Una funzione restituisce `false` e non riproduce nulla per un valore fuori
intervallo, un'opzione di tipo errato (`speed = "9"`, `explodes = 1`), un'opzione
sconosciuta o un effetto senza grafica né particella. Un argomento di tipo errato
genera un errore, come per ogni modulo. Un client classico non disegna particelle:
riceve la grafica e nulla per un effetto di sole particelle; un effetto in movimento
con grafica `1`, il segnaposto ModernUO, conta come tale. Un fulmine non ha grafica
e viene sempre inviato. Gli effetti non sono sequenziati: concatenali con chiamate `timer`.

`effect.at` riproduce una grafica effetto che resta su un punto della mappa, come
il fumo di un teletrasporto:

```lua
effect.at(MapType.Trammel, 1600, 1628, 5, EffectGraphicType.Smoke)
```
