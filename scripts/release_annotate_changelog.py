#!/usr/bin/env python3

from __future__ import annotations

import argparse
import re
import subprocess
from pathlib import Path


VERSION_HEADER_PATTERN = r"^## \[" + "{version}" + r"\]"
NEXT_HEADER_PATTERN = re.compile(r"^## \[", re.MULTILINE)
CO_AUTHOR_PATTERN = re.compile(r"^Co-authored-by:\s*(?P<name>[^<\n]+)", re.IGNORECASE | re.MULTILINE)


def run_git(*args: str, cwd: Path) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=cwd,
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout


def get_previous_tag(version: str, cwd: Path) -> str | None:
    tags_output = run_git("tag", "--sort=-creatordate", "--merged", "HEAD", "v*", cwd=cwd)
    tags = [tag.strip() for tag in tags_output.splitlines() if tag.strip() and tag.strip() != f"v{version}"]

    return tags[0] if tags else None


def get_commit_range(version: str, cwd: Path) -> str:
    previous_tag = get_previous_tag(version, cwd)

    if previous_tag:
        return f"{previous_tag}..HEAD"

    return "HEAD"


def collect_contributors(commit_range: str, cwd: Path) -> list[str]:
    commit_ids_output = run_git("rev-list", "--reverse", commit_range, cwd=cwd)
    commit_ids = [commit_id.strip() for commit_id in commit_ids_output.splitlines() if commit_id.strip()]
    ordered_unique: list[str] = []
    seen: set[str] = set()

    for commit_id in commit_ids:
        author = run_git("show", "-s", "--format=%aN", commit_id, cwd=cwd).strip()
        body = run_git("show", "-s", "--format=%B", commit_id, cwd=cwd)

        for contributor in [author, *CO_AUTHOR_PATTERN.findall(body)]:
            normalized = contributor.strip()
            normalized_key = normalized.casefold()

            if not normalized or "bot" in normalized_key or normalized_key in seen:
                continue

            seen.add(normalized_key)
            ordered_unique.append(normalized)

    return ordered_unique


def annotate_changelog(version: str, changelog_path: Path, contributors: list[str]) -> None:
    changelog_text = changelog_path.read_text(encoding="utf-8")
    header_pattern = re.compile(VERSION_HEADER_PATTERN.format(version=re.escape(version)), re.MULTILINE)
    header_match = header_pattern.search(changelog_text)

    if header_match is None:
        raise RuntimeError(f"Could not find changelog section for version {version}")

    section_start = header_match.start()
    next_match = NEXT_HEADER_PATTERN.search(changelog_text, header_match.end())
    section_end = next_match.start() if next_match else len(changelog_text)
    section_text = changelog_text[section_start:section_end].rstrip()

    existing_contributors_index = section_text.find("\n### Contributors\n")

    if existing_contributors_index >= 0:
        section_text = section_text[:existing_contributors_index].rstrip()

    if contributors:
        contributor_block = "\n\n### Contributors\n\n" + "\n".join(f"- {contributor}" for contributor in contributors)
        section_text = f"{section_text}{contributor_block}"

    updated_text = f"{changelog_text[:section_start]}{section_text}\n\n{changelog_text[section_end:].lstrip()}"
    changelog_path.write_text(updated_text, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Annotate the latest changelog section with contributors.")
    parser.add_argument("version", help="Release version without the leading v prefix.")
    parser.add_argument(
        "--changelog",
        default="CHANGELOG.md",
        help="Path to the changelog file to annotate.",
    )
    args = parser.parse_args()

    root = Path(__file__).resolve().parent.parent
    changelog_path = (root / args.changelog).resolve()
    contributors = collect_contributors(get_commit_range(args.version, root), root)
    annotate_changelog(args.version, changelog_path, contributors)

    print(f"Annotated {changelog_path.name} for {args.version} with {len(contributors)} contributor(s).")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
