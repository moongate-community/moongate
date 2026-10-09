"""UOX3's NPC lists and spawn regions as ``templates/npc_lists`` and ``templates/spawns`` (the spawn pass of ``uox``). Not ported yet."""

from __future__ import annotations

from pathlib import Path
from typing import TextIO


def run(
    mobile_source: Path,
    mobile_destination: Path,
    npc_lists_destination: Path,
    spawns_destination: Path,
    output: TextIO,
    error: TextIO,
) -> int:
    """Converts the ``[NPCLIST ...]`` blocks of ``npc/`` of UOX3's ``dfndata`` folder (``mobile_source``) into npc lists under
    ``npc_lists_destination``, and the ``[REGIONSPAWN ...]`` blocks of ``spawn/`` into spawn regions under ``spawns_destination``, one folder
    per map. ``mobile_destination`` is where the mobile pass wrote the mobile templates, which the lists and spawns name. Writes its report to
    ``output``; returns 0 when done, 2 for a bad source (nothing written)."""
    raise NotImplementedError("the UOX3 spawn pass is not ported yet")
