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
close it.

- Its owner lifts, drops and uses what is inside only while it is open: a closed bank refuses the
  lift, bounces what is dropped into it and opens nothing.
- Nobody else reaches it; game masters and administrators reach their own even while it is
  closed.
- The bank box itself never leaves the bank layer, and `world.carries` does not look in it: a key
  in the bank does not open a door.

## Gold by speech

Within 12 tiles of a banker, with the box open or not:

| Say | The banker | It answers |
| --- | --- | --- |
| *balance* | tells the gold in your bank | `Thy current bank balance is 1,234 gold.` |
| *withdraw 500* | moves 500 coins from the bank to your backpack | `Thou hast withdrawn gold from thy account.` |
| *deposit 500* | moves 500 coins from your backpack to the bank | `500 gold was deposited in your account.` |

- *balance* and *withdraw* are speech keywords of the client, like *bank*: they work in the
  language of the client. *deposit* is an English word only, because the client has no keyword
  for it.
- The amount is the first number of the sentence: `withdraw 500`, `I wish to withdraw 500 gold`
  and `500 withdraw` are the same. A sentence with no number, or with 0, moves nothing and gets
  no answer. Separators are not read: `5,000` is 5.
- The balance counts the coins anywhere in the bank box, bags included.
- A withdrawal is at most [`max_withdraw`](#settings) coins. They join a gold pile of your backpack
  when it has the room, a pile holding 60000 at most, and make a new pile otherwise.
- A deposit takes coins from the backpack and its bags; in the bank they top up the piles of the
  box, then make piles of 60000.
- Both are all or nothing: a refusal moves no coin.
- The banker's lines are the client's own texts, so every player reads them in its language.
- When several bankers hear you, one answers and the gold moves once.

| The banker says | Why |
| --- | --- |
| `Thou art a criminal and cannot access thy bank box.` | A criminal said *bank*. |
| `I will not do business with a criminal!` | A criminal asked for anything else. |
| `Thou canst not withdraw so much at one time!` | More than `max_withdraw`. |
| `Ah, art thou trying to fool me? Thou hast not so much gold!` | The bank, or the backpack for a deposit, has less than that. |
| `Your backpack can't hold anything else.` | The backpack cannot take the weight of the gold, or has no room for a new pile. |
| `Your bank box is full.` | The deposit needs a new pile and the box holds its [limit of items](#how-much-it-holds). |

A player who says *deposit* before ever opening its bank gets the box made and shown instead, and
asks again. Bank checks and deposits by handing the gold to the banker are not built yet.

## How much it holds

A bank box holds [`max_items`](#settings) items, 125 unless the shard says otherwise, counted
with everything inside its bags: a bag with ten things in it is eleven. A drop that would go past
the limit bounces back and you read `That container cannot hold more items.`

- Putting coins or anything that stacks onto a pile already there adds no item.
- An item dropped into a bag that is inside the bank box counts against the box too.
- Game masters and above are exempt.
- The box has no limit of weight, and what is in it weighs nothing on its owner.

The same rule holds for any container whose [item template](templates.md) sets `max_items`; a
template that says nothing has no limit. The gift of a script (`item.give`) into a backpack with
no room gives nothing.

## Settings

```toml
[ultima.bank]
max_items = 125        # Items in a bank box, bags included; 0 for no limit.
max_withdraw = 60000   # Coins a banker hands out at one time.
min_check = 5000       # The smallest check a banker writes.
max_check = 1000000    # The largest.
```

`max_items` goes from 0 to 10000, `max_withdraw` from 1 to 60000 (one pile), `min_check` from 1 to
`max_check`, `max_check` up to 2,000,000,000. The two check settings are read once bank checks
exist.

## The banker script

`scripts/mobiles/banker.lua` holds the rules above:
which word is which command, the distance, the criminal, the amount, and which text of the client
answers what. It is yours to change. It stands on these functions:

```lua
function banker.on_speech(serial, speaker, text, keywords)
    -- One banker serves when several hear the words.
    if not bank.attend(speaker) then
        return
    end

    if mobile.criminal(speaker) then
        npc.say_cliloc(serial, 500389) -- I will not do business with a criminal!
        return
    end

    -- A banker never walks: it turns to who asks.
    npc.look_at(serial, speaker)

    if bank.withdraw(speaker, 500) == BankResultType.Ok then
        npc.say_cliloc(serial, 1010005) -- Thou hast withdrawn gold from thy account.
    end
end
```

The banker turns to who asks (`npc.look_at`): an NPC is born facing south, and a banker never walks,
so without it every banker of a bank would face the same way for ever.

`on_speech` gets the speech keywords the client found as a fourth argument, an array of numbers;
`SpeechKeywordType` names the bank's (`Withdraw`, `Balance`, `Bank`, `Check`).

| Function | What |
| --- | --- |
| `bank.open(player)`, `bank.is_open(player)` | Opens the bank box; whether it is open. |
| `bank.balance(player)` | The gold in the bank; 0 for a player who never opened it. |
| `bank.withdraw(player, amount)` | Coins to the backpack; a `BankResultType`. |
| `bank.deposit(player, amount)` | Coins from the backpack; a `BankResultType`. |
| `bank.attend(player)` | True for the first banker that asks in the same moment. |
| `npc.say_cliloc(npc, cliloc [, args])` | The NPC says a text of the client, each player in its language. |

`BankResultType` is `Ok`, `NotEnoughGold`, `TooMuch`, `BackpackFull`, `BankFull`, `BadAmount`,
`NoPlayer`, `NoBank` (the player never opened its bank) or `Busy` (try again in a moment). The
module does not check where the player stands nor who it is: the script does. See the
[`bank` module](https://moongate.sh/lua/bank/).

## See also

- [NPC spawns](spawns.md)
- [Writing Lua scripts](scripting.md)
