import json
import tempfile
import unittest
from pathlib import Path

from scripts.sync_modernuo_books import (
    scan_books_metadata,
    sync_books_template_file,
    write_book_content_files,
)


class ScanBooksMetadataTests(unittest.TestCase):
    def test_extracts_defined_book_content_and_resolves_item_id(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "LibraryBooks.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class GrammarOfOrcish : BaseBook
                    {
                        public static readonly BookContent Content = new(
                            "A Grammar of Orcish",
                            "Yorick of Yew",
                            new BookPageInfo("Line 1", "Line 2"),
                            new BookPageInfo("Line 3", "", "Line 5")
                        );

                        [Constructible]
                        public GrammarOfOrcish() : base(Utility.Random(0xFEF, 2), false)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_books_metadata(root)
            defined = metadata["defined"]["grammar_of_orcish"]

            self.assertEqual("0x0FEF", defined["itemId"])
            self.assertEqual("grammar_of_orcish", defined["bookId"])
            self.assertEqual("A Grammar of Orcish", defined["title"])
            self.assertEqual("Yorick of Yew", defined["author"])
            self.assertEqual("Line 1\nLine 2\nLine 3\n\nLine 5", defined["content"])
            self.assertTrue(defined["readOnly"])

    def test_extracts_defined_book_item_id_from_colored_base_book(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "BlueBook.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class BlueBook : BaseBook
                    {
                        [Constructible]
                        public BlueBook() : base(0xFF2, 40)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "DrakovsJournal.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class DrakovsJournal : BlueBook
                    {
                        public static readonly BookContent Content = new(
                            "Drakov's Journal",
                            "Drakov",
                            new BookPageInfo("One", "Two")
                        );

                        [Constructible]
                        public DrakovsJournal() : base(false)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_books_metadata(root)
            defined = metadata["defined"]["drakovs_journal"]

            self.assertEqual("0x0FF2", defined["itemId"])

    def test_extracts_writable_blank_colored_books(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            root = Path(tmp_dir)
            (root / "BlueBook.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class BlueBook : BaseBook
                    {
                        [Constructible]
                        public BlueBook() : base(0xFF2, 40)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )
            (root / "BrownBook.cs").write_text(
                """
                namespace Server.Items
                {
                    public partial class BrownBook : BaseBook
                    {
                        [Constructible]
                        public BrownBook() : base(0xFEF)
                        {
                        }
                    }
                }
                """,
                encoding="utf-8",
            )

            metadata = scan_books_metadata(root)
            blue = metadata["blank"]["blue_book"]
            brown = metadata["blank"]["brown_book"]

            self.assertEqual("0x0FF2", blue["itemId"])
            self.assertEqual(40, blue["pages"])
            self.assertEqual("0x0FEF", brown["itemId"])
            self.assertEqual(20, brown["pages"])


class SyncBooksTemplateFileTests(unittest.TestCase):
    def test_updates_defined_and_blank_books(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "books.json"
            template_file.write_text(
                json.dumps(
                    [
                        {
                            "type": "item",
                            "id": "grammar_of_orcish",
                            "name": "Grammar Of Orcish",
                            "itemId": "0x0002",
                            "scriptId": "none",
                        },
                        {
                            "type": "item",
                            "id": "blue_book",
                            "name": "Blue Book",
                            "itemId": "0x0FF2",
                            "scriptId": "none",
                        },
                    ],
                    indent=2,
                ),
                encoding="utf-8",
            )

            sync_books_template_file(
                template_file,
                {
                    "defined": {
                        "grammar_of_orcish": {
                            "itemId": "0x0FEF",
                            "bookId": "grammar_of_orcish",
                            "title": "A Grammar of Orcish",
                            "author": "Yorick of Yew",
                            "content": "Line 1",
                            "readOnly": True,
                        }
                    },
                    "blank": {
                        "blue_book": {
                            "itemId": "0x0FF2",
                            "pages": 40,
                        }
                    },
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            grammar = next(item for item in data if item["id"] == "grammar_of_orcish")
            blue = next(item for item in data if item["id"] == "blue_book")

            self.assertEqual("", grammar["description"])
            self.assertEqual("grammar_of_orcish", grammar["bookId"])
            self.assertNotIn("params", grammar)
            self.assertEqual("0x0FEF", grammar["itemId"])
            self.assertEqual("", blue["description"])
            self.assertNotIn("bookId", blue)
            self.assertEqual("true", blue["params"]["writable"]["value"])
            self.assertEqual("40", blue["params"]["pages"]["value"])

    def test_appends_missing_defined_book_templates(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            template_file = Path(tmp_dir) / "books.json"
            template_file.write_text("[]\n", encoding="utf-8")

            sync_books_template_file(
                template_file,
                {
                    "defined": {
                        "drakovs_journal": {
                            "itemId": "0x0FF2",
                            "bookId": "drakovs_journal",
                            "title": "Drakov's Journal",
                            "author": "Drakov",
                            "content": "One\nTwo",
                            "readOnly": True,
                        }
                    },
                    "blank": {},
                },
            )

            data = json.loads(template_file.read_text(encoding="utf-8"))
            self.assertEqual(1, len(data))
            self.assertEqual("drakovs_journal", data[0]["id"])
            self.assertEqual("Drakov's Journal", data[0]["name"])
            self.assertEqual("", data[0]["description"])
            self.assertEqual("drakovs_journal", data[0]["bookId"])


class WriteBookContentFilesTests(unittest.TestCase):
    def test_writes_moongate_book_template_files(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            output_root = Path(tmp_dir)
            write_book_content_files(
                output_root,
                {
                    "grammar_of_orcish": {
                        "title": "A Grammar of Orcish",
                        "author": "Yorick of Yew",
                        "content": "Line 1\nLine 2",
                        "readOnly": True,
                    }
                },
            )

            content = (output_root / "grammar_of_orcish.txt").read_text(encoding="utf-8")
            self.assertIn("[Title] A Grammar of Orcish", content)
            self.assertIn("[Author] Yorick of Yew", content)
            self.assertIn("[ReadOnly] True", content)
            self.assertTrue(content.rstrip().endswith("Line 1\nLine 2"))

    def test_removes_orphan_generated_book_files_but_keeps_preserved_templates(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            output_root = Path(tmp_dir)
            (output_root / "life_of_atravelling_minstrel.txt").write_text("old", encoding="utf-8")
            (output_root / "welcome_player.txt").write_text("keep", encoding="utf-8")

            write_book_content_files(
                output_root,
                {
                    "life_of_a_travelling_minstrel": {
                        "title": "The Life of a Travelling Minstrel",
                        "author": "The Unknown Bard",
                        "content": "Line 1",
                        "readOnly": True,
                    }
                },
            )

            self.assertFalse((output_root / "life_of_atravelling_minstrel.txt").exists())
            self.assertTrue((output_root / "life_of_a_travelling_minstrel.txt").exists())
            self.assertEqual("keep", (output_root / "welcome_player.txt").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
