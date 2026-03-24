#!/usr/bin/env python3

import argparse
import sys
from pathlib import Path
from typing import List, Optional

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from scripts.modernuo_item_template_tooling import (
    MODERNUO_ITEMS_ROOT,
    ROOT_ITEMS_DIRECTORY,
    migrate_modernuo_item_templates,
)


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Move ModernUO item template files into the root items directory and normalize script ids.")
    parser.add_argument("--source-root", default=str(MODERNUO_ITEMS_ROOT))
    parser.add_argument("--target-root", default=str(ROOT_ITEMS_DIRECTORY))
    return parser


def main(argv: Optional[List[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(argv)
    migrate_modernuo_item_templates(Path(args.source_root), Path(args.target_root))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
