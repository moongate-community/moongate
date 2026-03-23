#!/usr/bin/env python3

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, Optional

if __package__ is None or __package__ == "":
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from scripts.modernuo_item_template_tooling import ROOT_ITEMS_DIRECTORY, normalize_script_id, normalize_template_description

CLASS_PATTERN = re.compile(
    r"(?P<prefix>(?:\[[^\]]+\]\s*)*)public\s+(?:abstract\s+)?partial\s+class\s+(?P<name>\w+)\s*:\s*(?P<base>\w+)",
    re.MULTILINE,
)
CONSTRUCTIBLE_CTOR_PATTERN = r"\[Constructible\]\s*public\s+{class_name}\s*\([^)]*\)\s*:\s*base\((?P<args>[^)]*)\)"
BOOK_CONTENT_PATTERN = re.compile(r"BookContent\s+Content\s*=\s*new\(", re.MULTILINE)

BLANK_BOOK_TEMPLATE_IDS = frozenset({"blue_book", "brown_book", "red_book", "tan_book"})
PRESERVED_BOOK_TEMPLATE_IDS = frozenset({"welcome_player"})


@dataclass(frozen=True)
class ClassDefinition:
    name: str
    base_name: str
    body: str


def to_snake_case(value: str) -> str:
    first_pass = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", value)
    second_pass = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", first_pass)
    return re.sub(r"[\s-]+", "_", second_pass).lower()


def load_json_array(path: Path) -> list[dict[str, object]]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError(f"{path} must contain a JSON array")

    return data


def write_json_array(path: Path, items: list[dict[str, object]]) -> None:
    path.write_text(f"{json.dumps(items, indent=2)}\n", encoding="utf-8")


def parse_class_definitions(root: Path) -> Dict[str, ClassDefinition]:
    definitions: Dict[str, ClassDefinition] = {}
    for path in sorted(root.rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        matches = list(CLASS_PATTERN.finditer(text))
        for index, match in enumerate(matches):
            start = match.start()
            end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
            definition = ClassDefinition(
                name=match.group("name"),
                base_name=match.group("base"),
                body=text[start:end],
            )
            definitions[definition.name] = definition

    return definitions


def find_constructible_base_args(definition: ClassDefinition) -> Optional[str]:
    pattern = re.compile(CONSTRUCTIBLE_CTOR_PATTERN.format(class_name=re.escape(definition.name)), re.MULTILINE | re.DOTALL)
    match = pattern.search(definition.body)
    if match is None:
        return None

    return match.group("args").strip()


def normalize_item_id(raw_value: str) -> str:
    value = int(raw_value, 0)
    return f"0x{value:04X}"


def extract_numeric_literals(expression: str) -> list[str]:
    return re.findall(r"0x[0-9A-Fa-f]+|\d+", expression)


def resolve_book_item_id(definitions: Dict[str, ClassDefinition], class_name: str, visited: Optional[set[str]] = None) -> Optional[str]:
    local_visited = visited or set()
    if class_name in local_visited:
        return None

    local_visited.add(class_name)
    definition = definitions.get(class_name)
    if definition is None:
        return None

    args = find_constructible_base_args(definition)
    if args is not None:
        literals = extract_numeric_literals(args)
        if literals:
            return normalize_item_id(literals[0])

    if definition.base_name == "BaseBook":
        return None

    return resolve_book_item_id(definitions, definition.base_name, local_visited)


def resolve_blank_book_pages(definitions: Dict[str, ClassDefinition], class_name: str, visited: Optional[set[str]] = None) -> int:
    local_visited = visited or set()
    if class_name in local_visited:
        return 20

    local_visited.add(class_name)
    definition = definitions.get(class_name)
    if definition is None:
        return 20

    args = find_constructible_base_args(definition)
    if args is not None:
        literals = extract_numeric_literals(args)
        if len(literals) >= 2:
            return int(literals[1], 0)

    if definition.base_name == "BaseBook":
        return 20

    return resolve_blank_book_pages(definitions, definition.base_name, local_visited)


def extract_balanced_segment(text: str, start_index: int, open_char: str = "(", close_char: str = ")") -> tuple[str, int]:
    depth = 0
    in_string = False
    escaped = False
    start_content = start_index + 1

    for index in range(start_index, len(text)):
        character = text[index]
        if in_string:
            if escaped:
                escaped = False
            elif character == "\\":
                escaped = True
            elif character == "\"":
                in_string = False
            continue

        if character == "\"":
            in_string = True
            continue

        if character == open_char:
            depth += 1
            continue

        if character == close_char:
            depth -= 1
            if depth == 0:
                return text[start_content:index], index

    raise ValueError("Unbalanced segment")


def decode_csharp_string(value: str) -> str:
    text = value[1:-1]
    return bytes(text, "utf-8").decode("unicode_escape")


def extract_string_literals(text: str) -> list[str]:
    literals: list[str] = []
    current: list[str] = []
    in_string = False
    escaped = False

    for character in text:
        if not in_string:
            if character == "\"":
                in_string = True
                current = ["\""]
            continue

        current.append(character)
        if escaped:
            escaped = False
            continue

        if character == "\\":
            escaped = True
            continue

        if character == "\"":
            literals.append(decode_csharp_string("".join(current)))
            in_string = False

    return literals


def parse_book_content(class_body: str) -> Optional[dict[str, object]]:
    match = BOOK_CONTENT_PATTERN.search(class_body)
    if match is None:
        return None

    open_parenthesis_index = class_body.find("(", match.end() - 1)
    if open_parenthesis_index < 0:
        return None

    content_args, _ = extract_balanced_segment(class_body, open_parenthesis_index)
    first_page_index = content_args.find("new BookPageInfo(")
    header_segment = content_args if first_page_index < 0 else content_args[:first_page_index]
    header_literals = extract_string_literals(header_segment)
    if len(header_literals) < 2:
        return None

    page_lines: list[str] = []
    search_index = 0
    page_marker = "new BookPageInfo("
    while True:
        marker_index = content_args.find(page_marker, search_index)
        if marker_index < 0:
            break

        open_index = content_args.find("(", marker_index)
        page_args, close_index = extract_balanced_segment(content_args, open_index)
        page_lines.extend(extract_string_literals(page_args))
        search_index = close_index + 1

    return {
        "title": header_literals[0],
        "author": header_literals[1],
        "content": "\n".join(page_lines),
        "readOnly": True,
    }


def scan_books_metadata(root: Path) -> Dict[str, Dict[str, Dict[str, object]]]:
    definitions = parse_class_definitions(root)
    defined: Dict[str, Dict[str, object]] = {}
    blank: Dict[str, Dict[str, object]] = {}

    for definition in definitions.values():
        template_id = to_snake_case(definition.name)
        item_id = resolve_book_item_id(definitions, definition.name)
        if item_id is None:
            continue

        content = parse_book_content(definition.body)
        if content is not None:
            defined[template_id] = {
                "itemId": item_id,
                "bookId": template_id,
                **content,
            }
            continue

        if template_id in BLANK_BOOK_TEMPLATE_IDS:
            blank[template_id] = {
                "itemId": item_id,
                "pages": resolve_blank_book_pages(definitions, definition.name),
            }

    return {"defined": defined, "blank": blank}


def sync_books_template_file(template_file: Path, metadata: Dict[str, Dict[str, Dict[str, object]]]) -> None:
    templates = load_json_array(template_file)
    templates_by_id = {str(template.get("id", "")): template for template in templates}
    defined = metadata["defined"]
    blank = metadata["blank"]

    for template in templates:
        template_id = str(template.get("id", ""))
        template["description"] = normalize_template_description(template.get("description"))
        if template_id in defined:
            book = defined[template_id]
            template["itemId"] = book["itemId"]
            template["bookId"] = book["bookId"]
            template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))
            template.pop("params", None)
            continue

        if template_id in blank:
            book = blank[template_id]
            template["itemId"] = book["itemId"]
            template["scriptId"] = normalize_script_id(str(template.get("scriptId", "")))
            template.pop("bookId", None)
            template["params"] = {
                "writable": {"type": "string", "value": "true"},
                "pages": {"type": "string", "value": str(book["pages"])},
            }

    for template_id, book in sorted(defined.items()):
        if template_id in templates_by_id:
            continue

        templates.append(
            {
                "type": "item",
                "id": template_id,
                "name": str(book["title"]),
                "category": "Books",
                "description": "",
                "itemId": book["itemId"],
                "hue": "0",
                "goldValue": "0",
                "weight": 0,
                "scriptId": "none",
                "bookId": book["bookId"],
                "isMovable": True,
                "tags": ["modernuo", "books"],
            }
        )

    templates.sort(key=lambda item: str(item.get("id", "")))
    write_json_array(template_file, templates)


def escape_book_text(value: str) -> str:
    return value.replace("\\", "\\\\").replace("#", "\\#")


def render_book_template(metadata: Dict[str, object]) -> str:
    title = escape_book_text(str(metadata["title"]))
    author = escape_book_text(str(metadata["author"]))
    content = "\n".join(escape_book_text(line) for line in str(metadata["content"]).split("\n"))
    return f"[Title] {title}\n[Author] {author}\n[ReadOnly] True\n\n{content}\n"


def write_book_content_files(output_root: Path, metadata: Dict[str, Dict[str, object]]) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    valid_book_ids = set(metadata.keys()) | PRESERVED_BOOK_TEMPLATE_IDS
    for existing_file in output_root.glob("*.txt"):
        if existing_file.stem in valid_book_ids:
            continue

        existing_file.unlink()

    for book_id, content in metadata.items():
        (output_root / f"{book_id}.txt").write_text(render_book_template(content), encoding="utf-8")


def discover_modernuo_root(repo_root: Path) -> Path:
    env_root = os.environ.get("MODERNUO_ROOT")
    candidates = []
    if env_root:
        candidates.append(Path(env_root).expanduser())

    candidates.extend(
        [
            repo_root.parent.parent / "others" / "ModernUO",
            repo_root.parent / "ModernUO",
            Path.home() / "projects" / "others" / "ModernUO",
        ]
    )

    for candidate in candidates:
        books_root = candidate / "Projects" / "UOContent" / "Items" / "Books"
        if books_root.exists():
            return candidate

    raise FileNotFoundError("Could not locate ModernUO root. Set MODERNUO_ROOT or pass --modernuo-root.")


def build_argument_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Sync ModernUO books into Moongate book templates.")
    parser.add_argument("--modernuo-root")
    parser.add_argument("--items-root", default=str(ROOT_ITEMS_DIRECTORY))
    parser.add_argument("--books-root", default="moongate_data/templates/books")
    return parser


def main(argv: Optional[Iterable[str]] = None) -> int:
    parser = build_argument_parser()
    args = parser.parse_args(list(argv) if argv is not None else None)
    repo_root = Path(__file__).resolve().parent.parent
    modernuo_root = Path(args.modernuo_root).expanduser() if args.modernuo_root else discover_modernuo_root(repo_root)
    metadata = scan_books_metadata(modernuo_root / "Projects" / "UOContent" / "Items" / "Books")
    sync_books_template_file(Path(args.items_root) / "books.json", metadata)
    write_book_content_files(Path(args.books_root), metadata["defined"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
