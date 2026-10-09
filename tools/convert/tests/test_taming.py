"""The taming converter."""

from __future__ import annotations

import io
import tomllib

from moongate_convert import taming
from moongate_convert.cli import main

HORSE = """
public partial class Horse : BaseMount
{
    [Constructible]
    public Horse() : base("a horse", 0xC8, 0x3E9F, AIType.AI_Animal, FightMode.Aggressor, 10, 1, 0.2, 0.4)
    {
        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 29.1;
    }
}
"""

DRAKE = """
public partial class Drake : BaseCreature
{
    public Drake() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
    {
        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 84.3;
    }
}
"""

WILD = """
public partial class Orc : BaseCreature
{
    public Orc() : base(AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4)
    {
        Tamable = false;
        MinTameSkill = 90.0;
    }
}
"""

NO_SKILL = """
public partial class Cat : BaseCreature
{
    public Cat() : base(AIType.AI_Animal, FightMode.Closest, 10, 1, 0.2, 0.4)
    {
        Tamable = true;
    }
}
"""

UNKNOWN = """
public partial class Unicorn : BaseCreature
{
    public Unicorn() : base(AIType.AI_Animal, FightMode.Closest, 10, 1, 0.2, 0.4)
    {
        Tamable = true;
        MinTameSkill = 101.1;
    }
}
"""

TEMPLATES = '[[mobile]]\nid = "horse"\n\n[[mobile]]\nid = "drake"\n\n[[mobile]]\nid = "orc"\n\n[[mobile]]\nid = "cat"\n'


def convert(tmp_path, sources):
    source = tmp_path / "Mobiles" / "Animals"
    source.mkdir(parents=True)

    for name, text in sources.items():
        (source / name).write_text(text, encoding="utf-8")

    templates = tmp_path / "templates"
    templates.mkdir()
    (templates / "animals.toml").write_text(TEMPLATES, encoding="utf-8")
    destination = tmp_path / "data"
    output, error = io.StringIO(), io.StringIO()
    code = main(
        ["modernuo-taming", "--source", str(tmp_path), "--templates", str(templates), "--destination", str(destination)],
        output,
        error,
    )

    return code, output.getvalue(), error.getvalue(), destination


def test_a_tamable_creature_becomes_an_entry_with_its_skill_and_slots(tmp_path):
    code, output, _, destination = convert(tmp_path, {"Horse.cs": HORSE, "Drake.cs": DRAKE})

    data = tomllib.loads((destination / "taming.toml").read_text(encoding="utf-8"))
    assert code == 0
    assert data["creature"] == [
        {"template": "drake", "min_skill": 84.3, "slots": 3, "food": ["meat"]},
        {"template": "horse", "min_skill": 29.1, "slots": 1, "food": ["meat"]},
    ]
    assert "taming.toml (2 creatures)" in output


def test_a_negative_skill_and_a_missing_one_are_kept_as_they_are_or_as_zero(tmp_path):
    cat = NO_SKILL
    bird = DRAKE.replace("Drake", "Bird").replace("84.3", "-6.9")
    _, _, _, destination = convert(tmp_path, {"Cat.cs": cat, "Bird.cs": bird})

    data = tomllib.loads((destination / "taming.toml").read_text(encoding="utf-8"))
    assert data["creature"] == [{"template": "cat", "min_skill": 0.0, "slots": 1, "food": ["meat"]}]


def test_the_coats_of_the_horse_take_its_skill(tmp_path):
    templates = TEMPLATES + '\n[[mobile]]\nid = "brownhorse"\n'
    source = tmp_path / "Mobiles"
    source.mkdir()
    (source / "Horse.cs").write_text(HORSE, encoding="utf-8")
    folder = tmp_path / "templates"
    folder.mkdir()
    (folder / "a.toml").write_text(templates, encoding="utf-8")
    code = main(
        ["modernuo-taming", "--source", str(tmp_path), "--templates", str(folder), "--destination", str(tmp_path / "data")],
        io.StringIO(),
        io.StringIO(),
    )

    data = tomllib.loads((tmp_path / "data" / "taming.toml").read_text(encoding="utf-8"))
    assert code == 0
    assert {creature["template"]: creature["min_skill"] for creature in data["creature"]} == {"horse": 29.1, "brownhorse": 29.1}


def test_what_is_not_tamable_or_has_no_template_or_no_skill_is_left_out_and_counted(tmp_path):
    code, output, _, destination = convert(
        tmp_path, {"Horse.cs": HORSE, "Orc.cs": WILD, "Cat.cs": NO_SKILL, "Unicorn.cs": UNKNOWN}
    )

    data = tomllib.loads((destination / "taming.toml").read_text(encoding="utf-8"))
    assert code == 0
    assert [creature["template"] for creature in data["creature"]] == ["cat", "horse"]
    assert "1 x class that sets Tamable to false" in output
    assert "1 x tamable class with no mobile template of this server" in output


def test_a_slots_value_that_is_no_literal_is_reported_and_taken_as_one(tmp_path):
    drake = DRAKE.replace("ControlSlots = 3;", "ControlSlots = Math.Max(1, 3);")

    _, output, _, destination = convert(tmp_path, {"Drake.cs": drake})

    data = tomllib.loads((destination / "taming.toml").read_text(encoding="utf-8"))
    assert data["creature"][0]["slots"] == 1
    assert "1 x ControlSlots that is no literal, taken as 1" in output


def test_an_integer_skill_is_read_as_a_number(tmp_path):
    drake = DRAKE.replace("84.3", "90")

    _, _, _, destination = convert(tmp_path, {"Drake.cs": drake})

    assert tomllib.loads((destination / "taming.toml").read_text(encoding="utf-8"))["creature"][0]["min_skill"] == 90.0


def test_no_tamable_creature_is_an_error_and_writes_nothing(tmp_path):
    code, _, error, destination = convert(tmp_path, {"Orc.cs": WILD})

    assert code == 2
    assert "no tamable creature" in error
    assert not (destination / "taming.toml").exists()


def test_a_missing_source_is_an_error(tmp_path):
    output, error = io.StringIO(), io.StringIO()

    code = main(
        ["modernuo-taming", "--source", str(tmp_path / "nope"), "--templates", str(tmp_path), "--destination", str(tmp_path / "out")],
        output,
        error,
    )

    assert code == 2
    assert "does not exist" in error.getvalue()


def test_template_ids_reads_every_toml_under_the_folder(tmp_path):
    (tmp_path / "a").mkdir()
    (tmp_path / "a" / "x.toml").write_text('[[mobile]]\nid = "horse"\nbase_id = "x"\n', encoding="utf-8")

    assert taming.template_ids(tmp_path) == {"horse"}


FED = """
public partial class Dog : BaseCreature
{
    public Dog() : base(AIType.AI_Animal, FightMode.Closest, 10, 1, 0.2, 0.4)
    {
        Tamable = true;
        MinTameSkill = 10.0;
    }

    public override FoodType FavoriteFood => FoodType.Fish | FoodType.Metal | FoodType.GrainsAndHay;
}
"""


def test_favorite_food_is_read_in_the_order_of_the_kinds_and_leaves_out_what_a_pet_does_not_eat(tmp_path):
    from pathlib import Path

    source = tmp_path / "Mobiles"
    source.mkdir()
    (source / "Dog.cs").write_text(FED, encoding="utf-8")
    (source / "Horse.cs").write_text(HORSE, encoding="utf-8")

    from moongate_convert import csharp

    found = {name: food for name, _, _, _, food in taming.read(csharp.read_source(source / "Dog.cs"), source / "Dog.cs", taming.ConversionReport())}
    assert found == {"Dog": ["grain", "fish"]}

    horse = taming.read(HORSE, Path("Horse.cs"), taming.ConversionReport())
    assert horse[0][4] == ["meat"]
