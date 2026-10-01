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
new login closes it. Turning in place does not.

- Its owner lifts, drops and uses what is inside only while it is open: a closed bank refuses the
  lift, bounces what is dropped into it and opens nothing.
- Nobody else reaches it; game masters and administrators always do.
- The bank box itself never leaves the bank layer, and `world.carries` does not look in it: a key
  in the bank does not open a door.

Withdrawing, the balance and bank checks are not built yet.

## The banker script

```lua
-- scripts/mobiles/banker.lua
function banker.on_speech(serial, speaker, text, keywords)
    if has_keyword(keywords, SpeechKeywordType.Bank) or text:lower():find("bank", 1, true) then
        bank.open(speaker)
    end
end
```

`on_speech` gets the speech keywords the client found as a fourth argument, an array of numbers;
`SpeechKeywordType` names the bank's (`Withdraw`, `Balance`, `Bank`, `Check`). Any mobile script
can open a bank with `bank.open(player)` and ask `bank.is_open(player)`; see
[Writing Lua scripts](scripting.md#available-host-functions).

## See also

- [NPC spawns](spawns.md)
- [Writing Lua scripts](scripting.md)
