#!/usr/bin/env python3
import argparse
import json
import sys
import urllib.request
from pathlib import Path

DISCORD_MESSAGE_LIMIT = 2000
DEFAULT_EMPTY_NOTES = "Release notes are available on GitHub."
USER_AGENT = "MoongateDiscordWebhook/1.0"


def normalize_notes(notes: str) -> str:
    normalized = notes.strip()
    return normalized if normalized else DEFAULT_EMPTY_NOTES


def chunk_text(text: str, limit: int) -> list[str]:
    chunks: list[str] = []
    remaining = text.strip()

    while remaining:
        if len(remaining) <= limit:
            chunks.append(remaining)
            break

        split_at = remaining.rfind("\n", 0, limit)
        if split_at <= 0:
            split_at = remaining.rfind(" ", 0, limit)
        if split_at <= 0:
            split_at = limit

        chunk = remaining[:split_at].rstrip()
        chunks.append(chunk)
        remaining = remaining[split_at:].lstrip()

    return chunks


def build_messages(version: str, repo: str, notes: str) -> list[dict[str, str]]:
    normalized_notes = normalize_notes(notes)
    release_url = f"https://github.com/{repo}/releases/tag/{version}"
    header = f"**{repo} {version}**\n{release_url}"
    note_chunks = chunk_text(normalized_notes, DISCORD_MESSAGE_LIMIT)
    messages = [{"content": header}]
    messages.extend({"content": chunk} for chunk in note_chunks)
    return messages


def post_messages(webhook_url: str, messages: list[dict[str, str]]) -> None:
    for message in messages:
        payload = json.dumps(message).encode("utf-8")
        request = urllib.request.Request(
            webhook_url,
            data=payload,
            headers={
                "Content-Type": "application/json",
                "User-Agent": USER_AGENT,
            },
            method="POST",
        )
        with urllib.request.urlopen(request, timeout=30) as response:
            response.read()


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Post GitHub release notes to a Discord webhook.")
    parser.add_argument("--version", required=True)
    parser.add_argument("--repo", required=True)
    parser.add_argument("--notes-file", required=True)
    parser.add_argument("--webhook-url", required=True)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(sys.argv[1:] if argv is None else argv)
    notes = Path(args.notes_file).read_text(encoding="utf-8")
    messages = build_messages(args.version, args.repo, notes)

    if args.dry_run:
        for message in messages:
            print(message["content"])
            print("---")
        return 0

    post_messages(args.webhook_url, messages)
    print(f"Posted {len(messages)} Discord changelog message(s) for {args.version}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
