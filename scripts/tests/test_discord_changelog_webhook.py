import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import scripts.discord_changelog_webhook as webhook


class BuildDiscordMessagesTests(unittest.TestCase):
    def test_build_messages_should_include_header_and_notes_chunks(self) -> None:
        notes = "## Features\n" + ("- Added a very long line that should be chunked safely.\n" * 80)

        messages = webhook.build_messages(
            version="v1.2.3",
            repo="moongate-community/moongate",
            notes=notes,
        )

        self.assertGreaterEqual(len(messages), 2)
        self.assertIn("moongate-community/moongate", messages[0]["content"])
        self.assertIn("v1.2.3", messages[0]["content"])
        self.assertIn("## Features", messages[1]["content"])
        self.assertTrue(all(len(message["content"]) <= webhook.DISCORD_MESSAGE_LIMIT for message in messages))

    def test_build_messages_should_normalize_empty_notes(self) -> None:
        messages = webhook.build_messages(
            version="v1.2.3",
            repo="moongate-community/moongate",
            notes="   \n",
        )

        self.assertEqual(2, len(messages))
        self.assertIn("Release notes are available", messages[1]["content"])


class PostMessagesTests(unittest.TestCase):
    @mock.patch("scripts.discord_changelog_webhook.urllib.request.urlopen")
    def test_post_messages_should_send_each_payload(self, mock_urlopen: mock.Mock) -> None:
        mock_urlopen.return_value.__enter__.return_value.read.return_value = b"ok"
        messages = [{"content": "first"}, {"content": "second"}]

        webhook.post_messages("https://discord.invalid/webhook", messages)

        self.assertEqual(2, mock_urlopen.call_count)
        first_request = mock_urlopen.call_args_list[0].args[0]
        self.assertEqual("application/json", first_request.headers["Content-type"])
        self.assertEqual(webhook.USER_AGENT, first_request.headers["User-agent"])
        self.assertEqual(
            {"content": "first"},
            json.loads(first_request.data.decode("utf-8")),
        )


class MainTests(unittest.TestCase):
    @mock.patch("scripts.discord_changelog_webhook.post_messages")
    def test_main_should_support_dry_run_without_network(self, post_messages: mock.Mock) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            notes_file = Path(tmp_dir) / "release_notes.md"
            notes_file.write_text("## Fixes\n- Improved teleport sync\n", encoding="utf-8")

            exit_code = webhook.main(
                [
                    "--version",
                    "v1.2.3",
                    "--repo",
                    "moongate-community/moongate",
                    "--notes-file",
                    str(notes_file),
                    "--webhook-url",
                    "https://discord.invalid/webhook",
                    "--dry-run",
                ]
            )

        self.assertEqual(0, exit_code)
        post_messages.assert_not_called()


if __name__ == "__main__":
    unittest.main()
