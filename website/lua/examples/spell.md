## cast

Begins the cast of a spell the mobile has in a book it carries, by key or by number; the usual checks tell it why not:

```lua
if spell.cast(user, "magic_arrow") then
    -- the words, the delay and the target cursor follow
end
```

## cast_scroll

What a double click on a scroll does, from the script of the scroll:

```lua
function spell_scroll.on_use(serial, user)
    spell.cast_scroll(user, serial)

    return true
end
```

## open_book

What a double click on a spellbook does:

```lua
function spellbook.on_use(serial, user)
    spell.open_book(user, serial)

    return true
end
```

## has

```lua
if not spell.has(book, "heal") then
    mobile.message(user, "Your book does not hold Heal.")
end
```

## find_book

The spellbook a mobile wears or carries that holds a spell, as a cast would find it; nil when it has none:

```lua
local book = spell.find_book(user, "recall")

if not book then
    mobile.message_cliloc(user, 1042404) -- You don't have that spell!
end
```

## add

Writes a spell in a book, as a scroll dropped on it does; false when the book holds it already:

```lua
spell.add(book, "recall")
spell.add(book, 32)
```

## spells

The numbers of the spells a book holds:

```lua
for _, id in ipairs(spell.spells(book)) do
    log.info(spell.info(id).name)
end
```

## info

What the data says of a spell:

```lua
local arrow = spell.info("magic_arrow")

mobile.message(user, arrow.name .. " costs " .. arrow.mana .. " mana")
```

## of_scroll

```lua
local id = spell.of_scroll(serial)

if id then
    mobile.message(user, "A scroll of " .. spell.info(id).name)
end
```

## is_casting

```lua
if spell.is_casting(user) then
    return false
end
```

## disturb

A curse may ruin the spell its target is casting, as damage does:

```lua
spell.disturb(target)
```

## cancel

Ends the cast with no message, such as when a player is teleported away. The short wait that the end of the delay set stays, so a cancel at the target cursor still leaves it:

```lua
spell.cancel(user)
```

## can_use_from_afar

Whether Telekinesis may use an item, before anything is spent:

```lua
function telekinesis.check(caster, target, info)
    if not spell.can_use_from_afar(caster, target.serial) then
        return 501857 -- This spell won't work on that!
    end
end
```

## use_from_afar

Uses an item as a double click would, from any distance: its script's `on_use` runs, or a container is shown open:

```lua
spell.use_from_afar(caster, target.serial)
```
