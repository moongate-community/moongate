"""Finds the Moongate mobile template of a ModernUO mobile class.

An alias of the table below, else the class in snake case (``GreatHart`` is ``great_hart``), else the class with the underscores of the ids
ignored, else UOX3's short name of an elemental (``DullCopperElemental`` is ``dullcopperele``).
"""

from __future__ import annotations

from collections.abc import Collection, Mapping

from .textutil import snake_case

ELEMENTAL_SUFFIX = "Elemental"

# Classes whose template has another name, or the nearest one of the same kind; keys compare without regard to case.
_ALIASES = {
    "armorer": "armourer",
    "barkeeper": "tavernkeeper",
    "bonedemon": "bonedaemon",
    "bonemagi": "bonemage",
    "customhairstylist": "hairstylist",
    "genericguard": "guard",
    "greathart": "hart",
    "grizzlybear": "grizbear",
    "headlessone": "headless",
    "hirebeggar": "beggar",
    "minter": "banker",
    "orcishlord": "orclord",
    "orcishmage": "orcmage",
    "ridablellama": "llama",
    "wanderinghealer": "healer",
}


def flat(name: str) -> str:
    """The lowercase form of a name without underscores, the key of the flattened ids."""
    return name.replace("_", "").lower()


def resolve(class_name: str, flat_ids: Mapping[str, str], ids: Collection[str]) -> str | None:
    """The template id of ``class_name`` among ``flat_ids`` (ids keyed by their flat form); None when there is none."""
    alias = _ALIASES.get(class_name.lower())

    if alias is not None and alias in ids:
        return alias

    snake = snake_case(class_name)

    if snake in ids:
        return snake

    if flat(class_name) in flat_ids:
        return flat_ids[flat(class_name)]

    if class_name.lower().endswith(ELEMENTAL_SUFFIX.lower()):
        return flat_ids.get(flat(class_name[: -len(ELEMENTAL_SUFFIX)]) + "ele")

    return None
