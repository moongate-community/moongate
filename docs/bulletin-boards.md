# Bulletin boards

A player double clicks a bulletin board, reads what others wrote on it, posts a message or a reply
and removes its own. Every board has its own messages: what is written on the board of Britain's
bank is not on the one of Minoc, nor on the other board of Britain.

The boards are the ones [`.decorate`](commands/decorate.md) places in the towns (the entries of
type `BulletinBoard` of the [decoration files](templates.md)), and any board a game master adds
with `.add bulletin_board`.

Moongate's own: how long a thread lasts and how many messages a board holds are settings; a full
board never refuses a post; a thread is removed with its replies. ModernUO deletes a thread six
hours after its last reply and leaves the replies of a removed message behind; Sphere keeps 32
messages and no threads; UOX3 lets only a game master remove.

## Read

Double click a board from two tiles or closer. The client shows the board and the list of its
messages: who wrote each, what about and on which day. Replies are listed under the first message
of their thread, in the order they were posted.

Open a message and you see its text and its poster as it looked when it posted: its body, its
colour and what it wore.

## Post

`Post` on the board opens an empty message: type a subject and the text. `Reply`, on a message,
posts under the thread of that message; a reply to a reply goes under the same first message.

- A post needs a subject and at least one line of text; otherwise nothing is posted.
- The subject is cut at 60 characters, a line at 80, a post at 32 lines. Control characters are
  taken out and the empty lines at the end dropped.
- You wait between two posts on one board: [`thread_seconds`](#settings) between two new threads,
  [`reply_seconds`](#settings) after any post before a reply. Too soon, you read `You must wait
  90 seconds before posting again.` and nothing is posted. Game masters and above do not wait.
  Removing your own post does not shorten the wait.
- A reply to a message that was removed meanwhile becomes a new thread.

## Remove

`Remove`, on a message, takes it off the board. You remove your own messages; a game master
removes any. Anyone else reads `That message is not yours.`

Removing the first message of a thread removes its replies with it.

## How long a message lasts

- A thread goes [`expire_days`](#settings) after its last reply: a thread people keep answering
  stays. With `expire_days = 0` nothing expires.
- A board holds [`max_messages`](#settings). A post that goes beyond them makes the board let go
  the thread left longest without a reply, with its replies. The thread you just replied to is
  never the one that goes: on a board with that thread alone, its oldest replies go.
- The server looks for expired threads when a board is opened and once an hour. That hourly look
  also drops the messages of a board that was deleted.

Messages are kept in the table `world.bulletin_messages` and written by the world save, so a
restart forgets none.

## Who may

Every request of the client is checked again by the server: the item is a bulletin board, your
character is on its map and within two tiles of it, and the message asked for is one of that
board. A request that fails is dropped without a word, as the other emulators do: walk away from a
board with its window open and its buttons do nothing. Game masters and above read and remove
from anywhere.

## Settings

```toml
[ultima.bulletin_boards]
expire_days = 7        # A thread goes this many days after its last reply; 0 keeps it.
max_messages = 50      # The messages a board holds; the oldest thread goes when it is full.
thread_seconds = 120   # The wait between two new threads of one character on a board.
reply_seconds = 30     # The wait between two posts of one character on a board.
```

`expire_days` goes from 0 to 3650, `max_messages` from 1 to 200, the two waits from 0 to 86400.

## The board item

| File | What |
| --- | --- |
| `templates/items/decorations.toml` | The item `bulletin_board`: graphic 0x1E5E, not movable, no decay, `script_id = "bulletin_board"` |
| `templates/items/building/decs/misc.toml` | `0x1e5e_bulletin_board` and `0x1e5f_bulletin_board`, the two facings, with the same script |
| `scripts/items/bulletin_board.lua` | Its [`on_use`](scripting/shipped-scripts.md#bulletin_boardlua) opens the board |

`.decorate` gives a `BulletinBoard` entry the template `bulletin_board` with the graphic of the
entry, and turns a board placed earlier as plain decoration into one. A `BountyBoard` stays a
thing to look at: bounties do not exist yet.

A board shows the name of its item, or `bulletin board` when it has none.

## For scripts

The [`board` module](https://moongate.sh/lua/board/) opens a board on a player, and lets a script
write on one, read it and remove from it:

```lua
function bulletin_board.on_use(serial, user)
    board.open(serial, user)

    return true
end
```

Any item whose template has `script_id = "bulletin_board"` is a board. `board.open` does not
check the distance: `on_use` already asks for two tiles and sight.

```lua
local notice = board.post(notice_board, "The town crier", "Hear ye", {
    "The bank of Britain is closed today.",
    "",
    "Come back tomorrow."
})

for _, message in ipairs(board.messages(notice_board)) do
    log.info(message.name .. ": " .. message.subject)
end

board.remove(notice)
```

- `board.post(board, name, subject, lines [, thread])` posts in the name given and gives the
  serial of the message, or `nil` when nothing was posted: no name, no subject, no line of text,
  an item that is not a board, or no serial ready for the message (the same call works a moment
  later). The lines end at the first `nil` among them. With `thread`, the serial of a message of that board, it is a
  reply. A script's post does not wait and has no poster: the message shows a bare body beside
  its text, and in the board's window only the staff removes it. The name is cut at 30
  characters; the subject, the lines and the size of the board follow the rules of a player's
  post.
- `board.messages(board)` gives the messages as an array of `{ serial, thread, poster, name,
  subject, lines, posted_at }`, the threads from the oldest, each followed by its replies.
  `thread` is `nil` on a first message, `poster` is `nil` for a message a script posted, and
  `posted_at` is in seconds, as `world.now()`.
- `board.remove(message)` removes a message, and its replies when it starts a thread; `false`
  when there is none. It asks nobody: a script that removes for a player checks `poster` first.

The module does not check who calls it: a script for the staff checks `world.is_staff` first. A
player who has the board open sees what a script posted or removed when it opens the board again.

## The packets

All of it travels in packet 0x71, with a sub-command:

| Sub-command | From | What |
| --- | --- | --- |
| 0x00 | server | The board and its name. The messages follow as the content of a container (0x3C), each an item 0x0EB0 inside the board |
| 0x01 | server | The summary of a message: poster, subject, date and the thread it replies to |
| 0x02 | server | A message in full: poster, subject, date, how the poster looked, the lines |
| 0x03 | client | Asks for the text of a message |
| 0x04 | client | Asks for the summary of a message |
| 0x05 | client | A post: what it replies to, the subject, the lines |
| 0x06 | client | The removal of a message |

A new message enters the poster's list as an item added to the board (0x25) and a message that
goes leaves it with 0x1D. The date is the day only, in English: `Oct 05, 2026`. The
[packet reference](https://moongate.sh/packets/) has every field.

## What it does not do yet

- Bounty boards, the boards of houses and the escort posts of UOX3.
- Moderation beyond removal: no locked thread, no pinned message, nobody banned from a board.
- A record of the messages removed.

## See also

- [Server configuration](server-configuration.md)
- [Shipped scripts](scripting/shipped-scripts.md#bulletin_boardlua)
- [Templates and decorations](templates.md)
