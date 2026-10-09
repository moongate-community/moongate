"""UOX3's ``[RANDOMNAME n]`` blocks as the name lists of ``names.toml``."""

from __future__ import annotations

from dataclasses import dataclass

from . import tomlout
from .dfn import DfnBlock
from .textutil import trim, try_int

HEADER_PREFIX = "RANDOMNAME "

# UOX3 numbers its lists; the ids name what uses them (list 12 is used by no NPC).
_LIST_IDS = {
    1: "male", 2: "female", 3: "orc", 4: "lizardman", 5: "daemon", 6: "ratman", 7: "balron", 8: "bird", 9: "ethereal_warrior",
    10: "centaur", 11: "pixie", 12: "list_12", 13: "fire_gargoyle", 14: "dark_father", 15: "darknight_creeper", 16: "impaler",
    17: "shadow_knight", 18: "golem_controller", 19: "savage", 20: "ancient_lich",
}  # fmt: skip

NAMES_HEADER = """# ==============================================================================
# Moongate - names.toml
#
# What it is for:
#   The lists random NPC names are drawn from. A mobile template names a list with
#   name_list (for example "male", or "{gender}" for the list of the gender the
#   mobile gets); the server picks one name from it at random.
#
# Fields:
#   id      the list id, unique ignoring case
#   names   the names; none may be empty
# ==============================================================================

"""


@dataclass
class NameList:
    id: str
    names: list[str]


def list_id(number: int) -> str:
    """The list id for UOX3's list number."""
    return _LIST_IDS.get(number, f"list_{number}")


def build(blocks: list[DfnBlock], dictionary: dict[int, str]) -> list[NameList]:
    """One list per ``[RANDOMNAME n]`` block, in the order of the blocks. A number is a dictionary id, else its ``//`` comment; duplicates are
    kept once."""
    lists: list[NameList] = []

    for block in blocks:
        if block.header[: len(HEADER_PREFIX)].lower() != HEADER_PREFIX.lower():
            continue

        number = try_int(trim(block.header[len(HEADER_PREFIX) :]))

        if number is None:
            continue

        names: list[str] = []

        for index, raw in enumerate(block.entries):
            entry = trim(raw)
            text_id = try_int(entry)
            name = entry

            if text_id is not None:
                name = dictionary.get(text_id)

                if name is None:
                    name = block.entry_comments[index]

            # string.IsNullOrWhiteSpace: a name of nothing but spaces is no name.
            if name is not None and trim(name) and name not in names:
                names.append(name)

        if names:
            lists.append(NameList(list_id(number), names))

    return lists


def serialize(lists: list[NameList]) -> str:
    """The ``[[names]]`` tables of ``names.toml`` (without the header)."""
    if not lists:
        return "names = []\n"

    return "\n".join(f"[[names]]\nid = {tomlout.basic(item.id)}\nnames = {tomlout.strings(item.names)}\n" for item in lists)
