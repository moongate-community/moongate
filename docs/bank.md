# Bank

Every player has a bank box, as in ModernUO: a container worn on the bank layer, which the client
never draws. A banker opens it when the player asks; the player then reaches what is inside until
he moves away.

## Opening the bank

Say *bank* near a banker. The client turns the word into the speech keyword
`SpeechKeywordType.Bank` in its own language, so *banca* on an Italian client works the same; the
shipped [`banker.lua`](#the-banker-script) also reads the plain word, for a client that sends no
keywords. The bank box opens with a line over the player:

```text
Bank container has 3 items.
```

The first time, the bank box is made (template `bank_box` of `templates/items/bank.toml`, a metal
chest graphic) and saved, then shown. The banker templates `banker`, `m_banker` and `f_banker` use
the script, so every banker of the [spawns](spawns.md) answers.

## While it is open

The bank stays open while the player stands where it opened: a step, a teleport, a map change or a
new login closes it, and coming back to the spot does not open it again. Turning in place does not
close it. When several bankers hear the same word, the bank shows once.

- Its owner lifts, drops and uses what is inside only while it is open: a closed bank refuses the
  lift, bounces what is dropped into it and opens nothing.
- Nobody else reaches it; game masters and administrators reach their own even while it is
  closed.
- The bank box itself never leaves the bank layer, and `world.carries` does not look in it: a key
  in the bank does not open a door.

Withdrawing, the balance and bank checks are not built yet.

## The banker script

```lua
-- scripts/mobiles/banker.lua
local function has_keyword(keywords, wanted)
    for _, keyword in ipairs(keywords or {}) do
        if keyword == wanted then
            return true
        end
    end

    return false
end

function banker.on_speech(serial, speaker, text, keywords)
    if has_keyword(keywords, SpeechKeywordType.Bank) or text:lower():find("bank", 1, true) then
        npc.look_at(serial, speaker)
        bank.open(speaker)
    end
end
```

The banker turns to who asks (`npc.look_at`): an NPC is born facing south, and a banker never walks,
so without it every banker of a bank would face the same way for ever.

`on_speech` gets the speech keywords the client found as a fourth argument, an array of numbers;
`SpeechKeywordType` names the bank's (`Withdraw`, `Balance`, `Bank`, `Check`). Any mobile script
can open a bank with `bank.open(player)` and ask `bank.is_open(player)`; see
the [`bank` module](https://moongate.sh/lua/bank/).

## See also

- [NPC spawns](spawns.md)
- [Writing Lua scripts](scripting.md)
