"""Regex-based parser for ModernUO C# NPC source files."""

import os
import re
from pathlib import Path
from typing import Dict, List, Optional, Tuple


CLASS_PATTERN = re.compile(
    r"public\s+(?:(?:abstract|partial)\s+)*class\s+(\w+)\s*:\s*(\w+)"
)
_PARENT_SOURCE_CACHE: Dict[Tuple[str, str], Optional[str]] = {}
DEFAULT_RANGE_PERCEPTION = 16
DEFAULT_RANGE_FIGHT = 1


def _parse_int_literal(value: str) -> int:
    return int(value, 16) if value.lower().startswith("0x") else int(value)


def _find_matching_delimiter(
    text: str, start: int, open_char: str, close_char: str
) -> int:
    depth = 0

    for index in range(start, len(text)):
        char = text[index]

        if char == open_char:
            depth += 1
        elif char == close_char:
            depth -= 1

            if depth == 0:
                return index

    return -1


def _strip_comments(text: str) -> str:
    without_block_comments = re.sub(r"/\*[\s\S]*?\*/", "", text)
    return re.sub(r"//.*$", "", without_block_comments, flags=re.MULTILINE)


def _extract_constructor_definitions(
    content: str,
    class_name: str,
    require_constructible: bool = True,
) -> List[dict]:
    content = _strip_comments(content)
    pattern = re.compile(
        rf"((?:\s*\[[^\]]+\]\s*)*)(?:public|protected|internal|private)\s+{re.escape(class_name)}\s*\("
    )
    constructors: List[dict] = []

    for match in pattern.finditer(content):
        attributes = match.group(1) or ""
        if require_constructible and not re.search(
            r"\[(?:Constructible|Constructable)\]",
            attributes,
        ):
            continue

        open_paren_index = content.find("(", match.end() - 1)
        if open_paren_index < 0:
            continue

        close_paren_index = _find_matching_delimiter(content, open_paren_index, "(", ")")
        if close_paren_index < 0:
            continue

        position = close_paren_index + 1
        initializer_kind = None
        initializer_arguments = None

        while position < len(content) and content[position].isspace():
            position += 1

        if position < len(content) and content[position] == ":":
            position += 1
            while position < len(content) and content[position].isspace():
                position += 1

            if content.startswith("base", position):
                initializer_kind = "base"
                position += 4
            elif content.startswith("this", position):
                initializer_kind = "this"
                position += 4

            if initializer_kind is not None:
                while position < len(content) and content[position].isspace():
                    position += 1

                if position >= len(content) or content[position] != "(":
                    continue

                initializer_close_index = _find_matching_delimiter(content, position, "(", ")")
                if initializer_close_index < 0:
                    continue

                initializer_arguments = content[position + 1 : initializer_close_index]
                position = initializer_close_index + 1

        while position < len(content) and content[position].isspace():
            position += 1

        body = None
        if content.startswith("=>", position):
            statement_start = position + 2
            statement_end = content.find(";", statement_start)
            if statement_end < 0:
                continue

            expression = content[statement_start:statement_end].strip()
            body = (expression + ";") if expression else ""
        else:
            if position >= len(content) or content[position] != "{":
                continue

            close_brace_index = _find_matching_delimiter(content, position, "{", "}")
            if close_brace_index < 0:
                continue

            body = content[position + 1 : close_brace_index]

        constructors.append(
            {
                "parameters": content[open_paren_index + 1 : close_paren_index],
                "initializer_kind": initializer_kind,
                "initializer_arguments": initializer_arguments,
                "body": body,
            }
        )

    return constructors


def _extract_constructor_body(
    content: str,
    class_name: str,
    require_constructible: bool = True,
) -> Optional[str]:
    constructors = _extract_constructor_definitions(
        content,
        class_name,
        require_constructible=require_constructible,
    )
    if not constructors:
        return None

    return constructors[0]["body"]


def _extract_base_arguments(
    content: str,
    class_name: str,
    require_constructible: bool = True,
) -> Optional[str]:
    constructors = _extract_constructor_definitions(
        content,
        class_name,
        require_constructible=require_constructible,
    )
    if not constructors:
        return None

    constructor = constructors[0]
    if constructor["initializer_kind"] != "base":
        return None

    return constructor["initializer_arguments"]


def _extract_constructible_constructor_body(
    content: str, class_name: str
) -> Optional[str]:
    return _extract_constructor_body(content, class_name, require_constructible=True)


def _extract_constructible_base_arguments(
    content: str, class_name: str
) -> Optional[str]:
    return _extract_base_arguments(content, class_name, require_constructible=True)


def _extract_method_body(content: str, method_name: str) -> Optional[str]:
    content = _strip_comments(content)
    pattern = re.compile(
        rf"(?:public|protected|private|internal)\s+(?:(?:override|virtual|sealed|new|static|partial)\s+)*[\w<>,\[\]?]+\s+{re.escape(method_name)}\s*\([^)]*\)\s*"
    )
    match = pattern.search(content)
    if match is None:
        return None

    position = match.end()
    while position < len(content) and content[position].isspace():
        position += 1

    if content.startswith("=>", position):
        statement_start = position + 2
        statement_end = content.find(";", statement_start)
        if statement_end < 0:
            return None

        expression = content[statement_start:statement_end].strip()
        if not expression:
            return ""

        return expression + ";"

    if position >= len(content) or content[position] != "{":
        return None

    open_brace_index = position
    close_brace_index = _find_matching_delimiter(content, open_brace_index, "{", "}")
    if close_brace_index < 0:
        return None

    return content[open_brace_index + 1 : close_brace_index]


def _extract_simple_variables(text: str) -> Dict[str, str]:
    variables: Dict[str, str] = {}

    for match in re.finditer(
        r"(?:var|int|bool|string|double)\s+(\w+)\s*=\s*([^;]+);",
        text,
    ):
        variables[match.group(1)] = match.group(2).strip()

    return variables


def _extract_class_simple_variables(text: str) -> Dict[str, str]:
    variables: Dict[str, str] = {}

    for match in re.finditer(
        r"(?m)^\s*(?:public|protected|private|internal)\s+(?:(?:static|readonly|const)\s+)*int\s+(\w+)\s*=\s*([^;]+);",
        _strip_comments(text),
    ):
        variables[match.group(1)] = match.group(2).strip()

    return variables


def _extract_top_level_statements(text: str) -> List[str]:
    statements: List[str] = []
    position = 0

    while position < len(text):
        while position < len(text) and text[position].isspace():
            position += 1

        if position >= len(text):
            break

        control_statement = _consume_control_statement(text, position)
        if control_statement is not None:
            statement, position = control_statement
        else:
            end = _find_statement_terminator(text, position)
            statement = text[position:end]
            position = end

        stripped = statement.strip()
        if stripped:
            statements.append(stripped)

    return statements


def _find_statement_terminator(statement_text: str, start: int) -> int:
    depth = 0
    for index in range(start, len(statement_text)):
        char = statement_text[index]
        if char in "({[":
            depth += 1
        elif char in ")}]":
            depth = max(0, depth - 1)
        elif char == ";" and depth == 0:
            return index + 1

    return len(statement_text)


def _consume_control_statement(statement_text: str, start: int) -> Optional[Tuple[str, int]]:
    remaining = statement_text[start:]
    switch_match = re.match(r"\s*switch\b", remaining)
    if switch_match is not None:
        brace_index = statement_text.find("{", start + switch_match.end())
        if brace_index < 0:
            return None

        close_brace_index = _find_matching_delimiter(statement_text, brace_index, "{", "}")
        if close_brace_index < 0:
            return None

        return statement_text[start : close_brace_index + 1], close_brace_index + 1

    if_match = re.match(r"\s*if\b", remaining)
    if if_match is None:
        return None

    condition_open_index = statement_text.find("(", start + if_match.end())
    if condition_open_index < 0:
        return None

    condition_close_index = _find_matching_delimiter(statement_text, condition_open_index, "(", ")")
    if condition_close_index < 0:
        return None

    if_open_brace = statement_text.find("{", condition_close_index)
    if if_open_brace < 0:
        return None

    if_close_brace = _find_matching_delimiter(statement_text, if_open_brace, "{", "}")
    if if_close_brace < 0:
        return None

    end = if_close_brace + 1
    tail = statement_text[end:]
    else_match = re.match(r"\s*else\b", tail)
    if else_match is None:
        return statement_text[start:end], end

    else_start = end + else_match.start()
    else_open_brace = statement_text.find("{", else_start + else_match.end())
    if else_open_brace < 0:
        return statement_text[start:end], end

    else_close_brace = _find_matching_delimiter(statement_text, else_open_brace, "{", "}")
    if else_close_brace < 0:
        return statement_text[start:end], end

    return statement_text[start : else_close_brace + 1], else_close_brace + 1


def _resolve_expression(
    expression: Optional[str], variables: Dict[str, str], depth: int = 0
) -> Optional[str]:
    if expression is None:
        return None

    resolved = expression.strip()
    if not resolved or depth >= 6:
        return resolved

    if resolved in variables:
        return _resolve_expression(variables[resolved], variables, depth + 1)

    if resolved.startswith("(") and resolved.endswith(")"):
        return _resolve_expression(resolved[1:-1], variables, depth + 1)

    return resolved


def _extract_assignment_expression(
    text: str, target: str, variables: Dict[str, str]
) -> Optional[str]:
    pattern = re.compile(rf"^\s*(?:this\.)?{re.escape(target)}\s*=\s*([^;]+);\s*$")
    nested_pattern = re.compile(
        rf"(?m)^\s*(?:this\.)?{re.escape(target)}\s*=\s*([^;]+);\s*$"
    )
    value = None

    for statement in _extract_top_level_statements(text):
        match = pattern.match(statement)
        if match is not None:
            value = match.group(1)
            continue

        if re.match(r"^\s*(?:if|switch)\b", statement):
            for nested_match in nested_pattern.finditer(statement):
                value = nested_match.group(1)

    return _resolve_expression(value, variables)


def _strip_named_argument(expression: str) -> str:
    stripped = expression.strip()
    named_match = re.match(r"(?:\w+\s*:|\w+\s*=)\s*(.+)$", stripped, re.S)
    if named_match is not None:
        return named_match.group(1).strip()

    return stripped


def _parse_ai_type_expression(
    expression: Optional[str],
    variables: Optional[Dict[str, str]] = None,
) -> Optional[str]:
    if expression is None:
        return None

    if variables is not None:
        expression = _resolve_expression(expression, variables)

    match = re.search(r"\bAIType\.(AI_\w+)\b", expression)
    if match is None:
        return None

    return match.group(1)


def _parse_fight_mode_expression(
    expression: Optional[str],
    variables: Optional[Dict[str, str]] = None,
) -> Optional[str]:
    if expression is None:
        return None

    if variables is not None:
        expression = _resolve_expression(expression, variables)

    match = re.search(r"\bFightMode\.(\w+)\b", expression)
    if match is None:
        return None

    return match.group(1)


def _parse_range_expression(
    expression: Optional[str],
    variables: Optional[Dict[str, str]] = None,
) -> Optional[int]:
    if expression is None:
        return None

    stripped = _strip_named_argument(expression)
    if variables is not None:
        stripped = _resolve_expression(stripped, variables) or ""

    if re.fullmatch(r"0x[0-9A-Fa-f]+|\d+", stripped):
        return _parse_int_literal(stripped)

    if stripped == "DefaultRangePerception":
        return DEFAULT_RANGE_PERCEPTION

    if stripped in {"DefaultRangeFight", "DefaultFightRange"}:
        return DEFAULT_RANGE_FIGHT

    return None


def _extract_ai_metadata(
    base_arguments: Optional[str],
    constructor_body: str,
    variables: Dict[str, str],
) -> dict:
    metadata = {}

    if base_arguments:
        arguments = [_strip_named_argument(argument) for argument in _split_top_level_arguments(base_arguments)]
        ai_index = -1

        for index, argument in enumerate(arguments):
            ai_type = _parse_ai_type_expression(argument, variables)
            if ai_type is None:
                continue

            metadata["ai_type"] = ai_type
            ai_index = index
            break

        if ai_index >= 0:
            trailing_arguments = arguments[ai_index + 1 :]
            if trailing_arguments:
                fight_mode = _parse_fight_mode_expression(trailing_arguments[0], variables)
                numeric_start = 0

                if fight_mode is not None:
                    metadata["fight_mode"] = fight_mode
                    numeric_start = 1

                range_arguments = trailing_arguments[numeric_start:]
                if range_arguments:
                    range_perception = _parse_range_expression(range_arguments[0], variables)
                    if range_perception is not None:
                        metadata["range_perception"] = range_perception

                if len(range_arguments) > 1:
                    range_fight = _parse_range_expression(range_arguments[1], variables)
                    if range_fight is not None:
                        metadata["range_fight"] = range_fight

    ai_assignment = _parse_ai_type_expression(
        _extract_assignment_expression(constructor_body, "AI", variables),
        variables,
    )
    if ai_assignment is not None:
        metadata["ai_type"] = ai_assignment

    fight_mode_assignment = _parse_fight_mode_expression(
        _extract_assignment_expression(constructor_body, "FightMode", variables),
        variables,
    )
    if fight_mode_assignment is not None:
        metadata["fight_mode"] = fight_mode_assignment

    range_perception_assignment = _parse_range_expression(
        _extract_assignment_expression(constructor_body, "RangePerception", variables),
        variables,
    )
    if range_perception_assignment is not None:
        metadata["range_perception"] = range_perception_assignment

    range_fight_assignment = _parse_range_expression(
        _extract_assignment_expression(constructor_body, "RangeFight", variables),
        variables,
    )
    if range_fight_assignment is not None:
        metadata["range_fight"] = range_fight_assignment

    if "ai_type" in metadata:
        metadata.setdefault("fight_mode", "Closest")
        metadata.setdefault("range_perception", DEFAULT_RANGE_PERCEPTION)
        metadata.setdefault("range_fight", DEFAULT_RANGE_FIGHT)

    return metadata


def _extract_style_assignment(text: str, target: str) -> Optional[int]:
    value = None
    pattern = re.compile(
        rf"^\s*(?:this\.)?{re.escape(target)}\s*=\s*(0x[0-9A-Fa-f]+|\d+);\s*$"
    )

    for statement in _extract_top_level_statements(text):
        match = pattern.match(statement)
        if match is not None:
            value = match.group(1)

    if value is None:
        return None

    return _parse_int_literal(value)


def _extract_name_assignment(text: str) -> Optional[str]:
    pattern = re.compile(
        r'^\s*(?:this\.)?Name\s*=\s*(?:NameList\.RandomName\s*\(\s*"(\w+)"\s*\)|"([^"]+)")\s*;\s*$'
    )
    value = None

    for statement in _extract_top_level_statements(text):
        match = pattern.match(statement)
        if match is not None:
            value = match.group(1) or match.group(2)

    return value


def _parse_body_choices_expression(
    expression: Optional[str],
    variables: Dict[str, str],
) -> Optional[List[int]]:
    resolved = _resolve_expression(expression, variables)
    if resolved is None:
        return None

    stripped = resolved.strip()
    if not stripped:
        return None

    if re.fullmatch(r"0x[0-9A-Fa-f]+|\d+", stripped):
        return [_parse_int_literal(stripped)]

    random_list_match = re.fullmatch(r"Utility\.RandomList\s*\((.*)\)", stripped)
    if random_list_match is None:
        ternary_parts = _split_top_level_ternary(stripped)
        if ternary_parts is None:
            return None

        _condition, truthy_expression, falsy_expression = ternary_parts
        truthy_choices = _parse_body_choices_expression(truthy_expression, variables)
        falsy_choices = _parse_body_choices_expression(falsy_expression, variables)
        if truthy_choices is None or falsy_choices is None:
            return None

        return [*truthy_choices, *falsy_choices]

    values = []
    for argument in _split_top_level_arguments(random_list_match.group(1)):
        argument = argument.strip()
        if not re.fullmatch(r"0x[0-9A-Fa-f]+|\d+", argument):
            return None

        values.append(_parse_int_literal(argument))

    return values or None


def _extract_appearance_assignments(
    text: str,
    variables: Dict[str, str],
) -> dict:
    appearance = {}

    body_choices = _parse_body_choices_expression(
        _extract_assignment_expression(text, "Body", variables),
        variables,
    )
    if body_choices:
        if len(body_choices) == 1:
            appearance["body"] = body_choices[0]
        else:
            appearance["body_options"] = body_choices

    skin_hue = _extract_assignment_expression(text, "Hue", variables)
    if skin_hue is not None:
        appearance["skin_hue"] = skin_hue

    hair_hue = _extract_assignment_expression(text, "HairHue", variables)
    if hair_hue is not None:
        appearance["hair_hue"] = hair_hue

    facial_hair_hue = _extract_assignment_expression(text, "FacialHairHue", variables)
    if facial_hair_hue is not None:
        appearance["facial_hair_hue"] = facial_hair_hue

    hair_style = _extract_style_assignment(text, "HairItemID")
    if hair_style is not None:
        appearance["hair_style"] = hair_style

    facial_hair_style = _extract_style_assignment(text, "FacialHairItemID")
    if facial_hair_style is not None:
        appearance["facial_hair_style"] = facial_hair_style

    return appearance


def _extract_new_expression_text(expression: str) -> Optional[Tuple[str, Optional[str], Optional[str]]]:
    stripped = expression.strip()
    match = re.match(r"new\s+(\w+)", stripped)
    if match is None:
        return None

    class_name = match.group(1)
    index = match.end()
    ctor_args = None
    initializer = None

    while index < len(stripped) and stripped[index].isspace():
        index += 1

    if index < len(stripped) and stripped[index] == "(":
        close_index = _find_matching_delimiter(stripped, index, "(", ")")
        if close_index < 0:
            return None

        ctor_args = stripped[index + 1 : close_index].strip() or None
        index = close_index + 1

    while index < len(stripped) and stripped[index].isspace():
        index += 1

    if index < len(stripped) and stripped[index] == "{":
        close_index = _find_matching_delimiter(stripped, index, "{", "}")
        if close_index < 0:
            return None

        initializer = stripped[index + 1 : close_index].strip() or None

    return class_name, ctor_args, initializer


def _split_top_level_arguments(arguments: str) -> List[str]:
    parts = []
    start = 0
    depth = 0

    for index, char in enumerate(arguments):
        if char in "({[":
            depth += 1
        elif char in ")}]":
            depth = max(0, depth - 1)
        elif char == "," and depth == 0:
            part = arguments[start:index].strip()
            if part:
                parts.append(part)
            start = index + 1

    tail = arguments[start:].strip()
    if tail:
        parts.append(tail)

    return parts


def _find_top_level_character(text: str, target: str) -> int:
    depth = 0

    for index, char in enumerate(text):
        if char in "({[":
            depth += 1
        elif char in ")}]":
            depth = max(0, depth - 1)
        elif char == target and depth == 0:
            return index

    return -1


def _parse_parameter_definitions(parameters: str) -> List[dict]:
    definitions: List[dict] = []

    for parameter in _split_top_level_arguments(parameters):
        stripped = parameter.strip()
        if not stripped:
            continue

        default_index = _find_top_level_character(stripped, "=")
        default_value = None
        declaration = stripped

        if default_index >= 0:
            declaration = stripped[:default_index].strip()
            default_value = stripped[default_index + 1 :].strip()

        declaration = re.sub(r"\[[^\]]+\]\s*", "", declaration).strip()
        name_match = re.search(r"(@?\w+)\s*$", declaration)
        if name_match is None:
            continue

        definitions.append(
            {
                "name": name_match.group(1).lstrip("@"),
                "default": default_value,
            }
        )

    return definitions


def _parse_named_call_argument(argument: str) -> Tuple[Optional[str], str]:
    stripped = argument.strip()
    named_match = re.match(r"^(@?\w+)\s*:\s*(.+)$", stripped, re.S)
    if named_match is not None:
        return named_match.group(1).lstrip("@"), named_match.group(2).strip()

    named_match = re.match(r"^(@?\w+)\s*=\s*(.+)$", stripped, re.S)
    if named_match is not None:
        return named_match.group(1).lstrip("@"), named_match.group(2).strip()

    return None, stripped


def _bind_arguments_to_parameters(
    arguments: Optional[str],
    parameters: List[dict],
    caller_variables: Dict[str, str],
) -> Optional[Tuple[Dict[str, str], int]]:
    bindings: Dict[str, str] = {}
    default_count = 0
    next_parameter_index = 0
    call_arguments = _split_top_level_arguments(arguments) if arguments else []

    for argument in call_arguments:
        name, value = _parse_named_call_argument(argument)
        resolved_value = _resolve_expression(value, caller_variables) or value.strip()

        if name is None:
            while (
                next_parameter_index < len(parameters)
                and parameters[next_parameter_index]["name"] in bindings
            ):
                next_parameter_index += 1

            if next_parameter_index >= len(parameters):
                return None

            bindings[parameters[next_parameter_index]["name"]] = resolved_value
            next_parameter_index += 1
            continue

        matching_parameter = next(
            (parameter for parameter in parameters if parameter["name"] == name),
            None,
        )
        if matching_parameter is None or name in bindings:
            return None

        bindings[name] = resolved_value

    for parameter in parameters:
        parameter_name = parameter["name"]
        if parameter_name in bindings:
            continue

        if parameter["default"] is None:
            return None

        bindings[parameter_name] = parameter["default"]
        default_count += 1

    return bindings, default_count


def _select_constructor_overload(
    constructors: List[dict],
    invocation_arguments: Optional[str],
    caller_variables: Dict[str, str],
) -> Optional[Tuple[dict, Dict[str, str]]]:
    best_match: Optional[Tuple[dict, Dict[str, str]]] = None
    best_score: Optional[Tuple[int, int]] = None

    for constructor in constructors:
        parameter_definitions = _parse_parameter_definitions(constructor["parameters"])
        bindings = _bind_arguments_to_parameters(
            invocation_arguments,
            parameter_definitions,
            caller_variables,
        )
        if bindings is None:
            continue

        parameter_bindings, default_count = bindings
        score = (default_count, len(parameter_definitions))
        if best_score is None or score < best_score:
            best_match = (constructor, parameter_bindings)
            best_score = score

    return best_match


def _split_top_level_ternary(expression: str) -> Optional[Tuple[str, str, str]]:
    depth = 0
    ternary_index = -1

    for index, char in enumerate(expression):
        if char in "({[":
            depth += 1
        elif char in ")}]":
            depth = max(0, depth - 1)
        elif char == "?" and depth == 0 and ternary_index < 0:
            ternary_index = index
        elif char == ":" and depth == 0 and ternary_index >= 0:
            return (
                expression[:ternary_index].strip(),
                expression[ternary_index + 1 : index].strip(),
                expression[index + 1 :].strip(),
            )

    return None


def _resolve_supported_item_hue_expression(
    expression: Optional[str], variables: Dict[str, str]
) -> Optional[str]:
    resolved = _resolve_expression(expression, variables)
    if resolved is None:
        return None

    if resolved == "Utility.RandomNeutralHue()":
        return resolved

    if re.fullmatch(r"0x[0-9A-Fa-f]+", resolved):
        return resolved

    if re.fullmatch(r"\d+", resolved):
        return resolved

    return None


def _extract_wrapped_item_hue_expression(
    expression: str,
    variables: Dict[str, str],
) -> Optional[str]:
    call_match = re.fullmatch(r"\w+\s*\((.*)\)", expression.strip(), re.S)
    if call_match is None:
        return None

    for argument in _split_top_level_arguments(call_match.group(1)):
        if re.search(r"\bnew\s+\w+", argument):
            continue

        hue_expression = _resolve_supported_item_hue_expression(argument, variables)
        if hue_expression is not None:
            return hue_expression

    return None


def _extract_new_expression_segment(text: str, start: int) -> Optional[Tuple[str, int]]:
    match = re.match(r"new\s+\w+", text[start:])
    if match is None:
        return None

    end = start + match.end()

    while end < len(text) and text[end].isspace():
        end += 1

    if end < len(text) and text[end] == "(":
        close_index = _find_matching_delimiter(text, end, "(", ")")
        if close_index < 0:
            return None
        end = close_index + 1

    while end < len(text) and text[end].isspace():
        end += 1

    if end < len(text) and text[end] == "{":
        close_index = _find_matching_delimiter(text, end, "{", "}")
        if close_index < 0:
            return None
        end = close_index + 1

    return text[start:end], end


def _extract_new_expressions(text: str) -> List[str]:
    expressions = []
    position = 0

    while True:
        match = re.search(r"\bnew\s+\w+", text[position:])
        if match is None:
            break

        start = position + match.start()
        extracted = _extract_new_expression_segment(text, start)
        if extracted is None:
            break

        expression, end = extracted
        expressions.append(expression)
        position = end

    return expressions


def _parse_new_expression(
    expression: str, variables: Dict[str, str]
) -> Optional[dict]:
    parsed = _extract_new_expression_text(expression)
    if parsed is None:
        return None

    class_name, ctor_args, initializer = parsed
    hue_expression = None

    if initializer:
        hue_match = re.search(r"\bHue\s*=\s*([^,]+)", initializer, re.S)
        if hue_match:
            hue_expression = _resolve_supported_item_hue_expression(
                hue_match.group(1).strip(),
                variables,
            )

    if hue_expression is None and ctor_args:
        args = _split_top_level_arguments(ctor_args)
        if len(args) == 1:
            hue_expression = _resolve_supported_item_hue_expression(args[0], variables)

    return {
        "class_name": class_name,
        "hue": hue_expression,
        "weight": 1,
    }


def _parse_add_item_expression(
    expression: str, variables: Dict[str, str]
) -> List[dict]:
    stripped = expression.strip()
    options: List[dict] = []

    if "switch" in stripped and "=> new" in stripped:
        expected_arms = len(re.findall(r"=>", stripped))
        new_expressions = _extract_new_expressions(stripped)
        if expected_arms != len(new_expressions):
            return []

        for new_expression in new_expressions:
            option = _parse_new_expression(new_expression, variables)
            if option is None:
                return []

            options.append(option)

        return options

    if "?" in stripped and ":" in stripped:
        ternary_options = _extract_new_expressions(stripped)
        if len(ternary_options) == 2:
            left = _parse_new_expression(ternary_options[0], variables)
            right = _parse_new_expression(ternary_options[1], variables)

            if left is None or right is None:
                return []

            options.append(left)
            options.append(right)

            return options

    option = _parse_new_expression(stripped, variables)
    if option:
        options.append(option)
        return options

    wrapped_expressions = _extract_new_expressions(stripped)
    if len(wrapped_expressions) == 1:
        wrapped_option = _parse_new_expression(wrapped_expressions[0], variables)
        if wrapped_option is not None:
            wrapped_hue = _extract_wrapped_item_hue_expression(stripped, variables)
            if wrapped_hue is not None:
                wrapped_option["hue"] = wrapped_hue
            options.append(wrapped_option)

    return options


def _extract_add_item_groups(
    text: str, variables: Dict[str, str]
) -> List[List[dict]]:
    def extract_nested_groups(statement_text: str) -> List[List[dict]]:
        nested_groups: List[List[dict]] = []
        position = 0

        while True:
            match = re.search(r"\b(?:AddItem|EquipItem)\s*\(", statement_text[position:])
            if match is None:
                break

            open_paren_index = position + match.end() - 1
            close_paren_index = _find_matching_delimiter(statement_text, open_paren_index, "(", ")")
            if close_paren_index < 0:
                break

            expression = statement_text[open_paren_index + 1 : close_paren_index]
            group = _parse_add_item_expression(expression, variables)
            if group:
                nested_groups.append(group)

            position = close_paren_index + 1

        return nested_groups

    groups: List[List[dict]] = []
    position = 0
    while position < len(text):
        while position < len(text) and text[position].isspace():
            position += 1

        if position >= len(text):
            break

        control_statement = _consume_control_statement(text, position)
        if control_statement is not None:
            statement, position = control_statement
        else:
            end = _find_statement_terminator(text, position)
            statement = text[position:end]
            position = end

        stripped = statement.lstrip()
        nested_groups = extract_nested_groups(statement)
        if not nested_groups:
            continue

        if re.match(r"^(?:if|switch)\b", stripped) and all(len(group) == 1 for group in nested_groups):
            groups.append([group[0] for group in nested_groups])
            continue

        groups.extend(nested_groups)

    return groups


def _extract_gender_variants(
    constructor_body: str,
) -> Tuple[List[dict], str]:
    inline_match = re.search(
        r"if\s*\(\s*Female\s*=\s*Utility\.RandomBool\(\)\s*\)",
        constructor_body,
    )
    split_assignment_match = None
    branch_match = inline_match
    before = None

    if inline_match is not None:
        before = constructor_body[: inline_match.start()]
        branch_search_start = inline_match.end()
    else:
        split_assignment_match = re.search(
            r"\bFemale\s*=\s*Utility\.RandomBool\(\)\s*;",
            constructor_body,
        )
        if split_assignment_match is None:
            return [], constructor_body

        split_if_match = re.search(
            r"\bif\s*\(\s*Female\s*\)",
            constructor_body[split_assignment_match.end() :],
        )
        if split_if_match is None:
            return [], constructor_body

        branch_match_start = split_assignment_match.end() + split_if_match.start()
        branch_match_end = split_assignment_match.end() + split_if_match.end()
        branch_match = re.match(
            r".*",
            constructor_body[branch_match_start:branch_match_end],
        )
        before = (
            constructor_body[: split_assignment_match.start()]
            + constructor_body[split_assignment_match.end() : branch_match_start]
        )
        branch_search_start = branch_match_end

    if branch_match is None or before is None:
        return [], constructor_body

    if_open_brace = constructor_body.find("{", branch_search_start)
    if if_open_brace < 0:
        return [], constructor_body

    if_close_brace = _find_matching_delimiter(constructor_body, if_open_brace, "{", "}")
    if if_close_brace < 0:
        return [], constructor_body

    else_match = re.search(r"\belse\b", constructor_body[if_close_brace + 1 :])
    if else_match is None:
        return [], constructor_body

    else_index = if_close_brace + 1 + else_match.start()
    else_open_brace = constructor_body.find("{", else_index)
    if else_open_brace < 0:
        return [], constructor_body

    else_close_brace = _find_matching_delimiter(
        constructor_body, else_open_brace, "{", "}"
    )
    if else_close_brace < 0:
        return [], constructor_body

    after = constructor_body[else_close_brace + 1 :]
    shared_body = before + "\n" + after
    base_variables = _extract_simple_variables(constructor_body)

    variants = []
    for name, block in [
        ("female", constructor_body[if_open_brace + 1 : if_close_brace]),
        ("male", constructor_body[else_open_brace + 1 : else_close_brace]),
    ]:
        variables = dict(base_variables)
        variables.update(_extract_simple_variables(block))
        appearance = _extract_appearance_assignments(block, variables)

        variants.append(
            {
                "name": name,
                "weight": 1,
                "appearance": appearance,
                "equipment_groups": _extract_add_item_groups(block, variables),
            }
        )

    return variants, shared_body


def _new_init_outfit_data() -> dict:
    return {
        "shared_equipment_groups": [],
        "shared_appearance": {},
        "variant_overrides": {},
    }


def _merge_init_outfit_data(target: dict, source: dict) -> None:
    target["shared_equipment_groups"].extend(source.get("shared_equipment_groups", []))
    target["shared_appearance"].update(source.get("shared_appearance", {}))

    for name, override in source.get("variant_overrides", {}).items():
        if not isinstance(name, str):
            continue

        target_override = target["variant_overrides"].setdefault(
            name,
            {"equipment_groups": [], "appearance": {}},
        )
        target_override["equipment_groups"].extend(override.get("equipment_groups", []))
        target_override["appearance"].update(override.get("appearance", {}))


def _extract_gender_conditional_blocks(statement: str) -> Optional[dict]:
    stripped = statement.lstrip()
    if not re.match(r"^if\b", stripped):
        return None

    start = len(statement) - len(stripped)
    condition_open_index = statement.find("(", start)
    if condition_open_index < 0:
        return None

    condition_close_index = _find_matching_delimiter(statement, condition_open_index, "(", ")")
    if condition_close_index < 0:
        return None

    condition = re.sub(r"\s+", "", statement[condition_open_index + 1 : condition_close_index])
    if condition in {"Female", "this.Female"}:
        truthy_name = "female"
        falsy_name = "male"
    elif condition in {"!Female", "!this.Female"}:
        truthy_name = "male"
        falsy_name = "female"
    else:
        return None

    if_open_brace = statement.find("{", condition_close_index)
    if if_open_brace < 0:
        return None

    if_close_brace = _find_matching_delimiter(statement, if_open_brace, "{", "}")
    if if_close_brace < 0:
        return None

    truthy_block = statement[if_open_brace + 1 : if_close_brace]
    falsy_block = None

    tail = statement[if_close_brace + 1 :]
    else_match = re.search(r"\belse\b", tail)
    if else_match is not None:
        else_start = if_close_brace + 1 + else_match.start()
        else_open_brace = statement.find("{", else_start)
        if else_open_brace >= 0:
            else_close_brace = _find_matching_delimiter(statement, else_open_brace, "{", "}")
            if else_close_brace >= 0:
                falsy_block = statement[else_open_brace + 1 : else_close_brace]

    return {
        truthy_name: truthy_block,
        falsy_name: falsy_block,
    }


def _extract_parent_init_outfit_data(
    filepath: str,
    base_class: str,
    visited: set[str],
) -> dict:
    parent_definition = _resolve_class_definition(filepath, base_class)
    if parent_definition is None:
        return _new_init_outfit_data()

    parent_source, resolved_class_name, parent_base_class, parent_content = parent_definition
    return _extract_init_outfit_data(
        parent_source,
        resolved_class_name,
        parent_base_class,
        parent_content,
        visited,
    )


def _extract_init_outfit_data(
    filepath: str,
    class_name: str,
    base_class: str,
    class_content: str,
    visited: set[str],
) -> dict:
    visit_key = f"{Path(filepath).resolve()}::{class_name}::InitOutfit"
    if visit_key in visited:
        return _new_init_outfit_data()

    visited.add(visit_key)

    method_body = _extract_method_body(class_content, "InitOutfit")
    if method_body is None:
        return _extract_parent_init_outfit_data(filepath, base_class, visited)

    data = _new_init_outfit_data()
    variables = _extract_simple_variables(method_body)

    for statement in _extract_top_level_statements(method_body):
        if re.fullmatch(r"\s*base\.InitOutfit\s*\(\s*\)\s*;", statement):
            parent_data = _extract_parent_init_outfit_data(filepath, base_class, visited)
            _merge_init_outfit_data(data, parent_data)
            continue

        gender_blocks = _extract_gender_conditional_blocks(statement)
        if gender_blocks is not None:
            for variant_name, block in gender_blocks.items():
                if not block:
                    continue

                block_variables = dict(variables)
                block_variables.update(_extract_simple_variables(block))
                override = data["variant_overrides"].setdefault(
                    variant_name,
                    {"equipment_groups": [], "appearance": {}},
                )
                override["equipment_groups"].extend(
                    _extract_add_item_groups(block, block_variables)
                )
                override["appearance"].update(
                    _extract_appearance_assignments(block, block_variables)
                )
            continue

        data["shared_equipment_groups"].extend(_extract_add_item_groups(statement, variables))
        data["shared_appearance"].update(
            _extract_appearance_assignments(statement, variables)
        )

    return data


def _build_base_class_variants(base_class: str) -> List[dict]:
    if base_class in {"BaseVendor", "BaseGuildmaster"}:
        bodies = (0x191, 0x190)
    elif base_class == "BaseEscortable":
        bodies = (401, 400)
    else:
        return []

    return [
        {
            "name": "female",
            "weight": 1,
            "appearance": {
                "body": bodies[0],
                "skin_hue": "Race.Human.RandomSkinHue()",
            },
            "equipment_groups": [],
        },
        {
            "name": "male",
            "weight": 1,
            "appearance": {
                "body": bodies[1],
                "skin_hue": "Race.Human.RandomSkinHue()",
            },
            "equipment_groups": [],
        },
    ]


def _find_mobiles_root(filepath: str) -> Path:
    source_path = Path(filepath).resolve()
    for parent in source_path.parents:
        if parent.name == "Mobiles":
            return parent

    return source_path.parent


def _extract_class_definitions(content: str) -> List[Tuple[str, str, str]]:
    definitions: List[Tuple[str, str, str]] = []

    for match in CLASS_PATTERN.finditer(content):
        class_name = match.group(1)
        base_class = match.group(2)
        open_brace_index = content.find("{", match.end())
        if open_brace_index < 0:
            continue

        close_brace_index = _find_matching_delimiter(content, open_brace_index, "{", "}")
        if close_brace_index < 0:
            continue

        definitions.append(
            (class_name, base_class, content[match.start() : close_brace_index + 1])
        )

    return definitions


def _find_class_definition(filepath: str, class_name: str) -> Optional[Tuple[str, str, str]]:
    with open(filepath, "r", encoding="utf-8-sig") as file_handle:
        content = file_handle.read()

    for candidate_class_name, base_class, class_content in _extract_class_definitions(content):
        if candidate_class_name == class_name:
            return candidate_class_name, base_class, class_content

    return None


def _resolve_class_definition(
    filepath: str,
    class_name: str,
) -> Optional[Tuple[str, str, str, str]]:
    class_definition = _find_class_definition(filepath, class_name)
    if class_definition is not None:
        resolved_class_name, base_class, class_content = class_definition
        return filepath, resolved_class_name, base_class, class_content

    class_source = _find_base_class_source_file(filepath, class_name)
    if class_source is None:
        return None

    class_definition = _find_class_definition(class_source, class_name)
    if class_definition is None:
        return None

    resolved_class_name, base_class, class_content = class_definition
    return class_source, resolved_class_name, base_class, class_content


def _find_base_class_source_file(filepath: str, base_class: str) -> Optional[str]:
    mobiles_root = _find_mobiles_root(filepath)
    cache_key = (str(mobiles_root), base_class)
    if cache_key in _PARENT_SOURCE_CACHE:
        return _PARENT_SOURCE_CACHE[cache_key]

    matches = list(mobiles_root.rglob(f"{base_class}.cs"))
    source_file = str(matches[0]) if matches else None
    _PARENT_SOURCE_CACHE[cache_key] = source_file
    return source_file


def _is_mobile_base_class(
    filepath: str,
    base_class: str,
    visited: Optional[set[str]] = None,
) -> bool:
    if visited is None:
        visited = set()

    if base_class == "Item":
        return False

    if base_class in {"BaseCreature", "BaseMount", "BaseVendor", "BaseEscortable", "BaseGuildmaster"}:
        return True

    if base_class.startswith("Base"):
        return True

    parent_definition = _resolve_class_definition(filepath, base_class)
    if parent_definition is None:
        return True

    parent_source, _parent_class_name, parent_base_class, _parent_content = parent_definition
    visit_key = f"{Path(parent_source).resolve()}::{base_class}"
    if visit_key in visited:
        return True

    visited.add(visit_key)
    return _is_mobile_base_class(parent_source, parent_base_class, visited)


def _inherit_parent_appearance(
    filepath: str,
    base_class: str,
    visited: set[str],
) -> dict:
    if base_class.startswith("Base"):
        default_variants = _build_base_class_variants(base_class)
        if default_variants:
            return {"variants": default_variants}

    parent_definition = _resolve_class_definition(filepath, base_class)
    if parent_definition is None:
        return {}

    parent_source, _resolved_class_name, _parent_base_class, _parent_content = parent_definition
    parent_data = _parse_named_class_from_file(parent_source, base_class, visited)
    if not parent_data:
        return {}

    inherited = {}
    if "variants" in parent_data:
        inherited["variants"] = parent_data["variants"]

    for key in (
        "body",
        "body_options",
        "skin_hue",
        "hair_hue",
        "hair_style",
        "facial_hair_hue",
        "facial_hair_style",
    ):
        if key in parent_data:
            inherited[key] = parent_data[key]

    return inherited


def _build_ai_metadata_variables(
    class_content: str,
    constructor_body: str,
    parameter_bindings: Optional[Dict[str, str]] = None,
) -> Dict[str, str]:
    variables = _extract_class_simple_variables(class_content)
    if parameter_bindings:
        variables.update(parameter_bindings)
    variables.update(_extract_simple_variables(constructor_body))
    return variables


def _parse_inheritable_ai_metadata(
    filepath: str,
    class_name: str,
    base_class: str,
    class_content: str,
    invocation_arguments: Optional[str],
    caller_variables: Dict[str, str],
    visited: set[str],
) -> dict:
    visit_key = f"{Path(filepath).resolve()}::{class_name}::ai::{invocation_arguments or '<default>'}"
    if visit_key in visited:
        return {}

    visited.add(visit_key)

    constructors = _extract_constructor_definitions(
        class_content,
        class_name,
        require_constructible=False,
    )
    if not constructors:
        return {}

    selected_constructor = _select_constructor_overload(
        constructors,
        invocation_arguments,
        caller_variables,
    )
    if selected_constructor is None:
        if len(constructors) != 1:
            return {}

        constructor = constructors[0]
        parameter_bindings: Dict[str, str] = {}
    else:
        constructor, parameter_bindings = selected_constructor

    constructor_body = constructor["body"] or ""
    variables = _build_ai_metadata_variables(
        class_content,
        constructor_body,
        parameter_bindings,
    )
    metadata = _extract_ai_metadata(
        constructor["initializer_arguments"]
        if constructor["initializer_kind"] == "base"
        else None,
        constructor_body,
        variables,
    )

    missing_keys = {
        key
        for key in ("ai_type", "fight_mode", "range_perception", "range_fight")
        if key not in metadata
    }
    if not missing_keys:
        return metadata

    inherited = _inherit_parent_ai_metadata(
        filepath,
        base_class,
        constructor["initializer_arguments"]
        if constructor["initializer_kind"] == "base"
        else None,
        variables,
        visited,
    )
    inherited.update(metadata)
    return inherited


def _inherit_parent_ai_metadata(
    filepath: str,
    base_class: str,
    invocation_arguments: Optional[str],
    caller_variables: Dict[str, str],
    visited: set[str],
) -> dict:
    parent_definition = _resolve_class_definition(filepath, base_class)
    if parent_definition is None:
        return {}

    parent_source, resolved_class_name, parent_base_class, parent_content = parent_definition
    return _parse_inheritable_ai_metadata(
        parent_source,
        resolved_class_name,
        parent_base_class,
        parent_content,
        invocation_arguments,
        caller_variables,
        visited,
    )


def _parse_class_definition(
    filepath: str,
    class_name: str,
    base_class: str,
    class_content: str,
    visited: set[str],
) -> Optional[dict]:
    visit_key = f"{Path(filepath).resolve()}::{class_name}"
    if visit_key in visited:
        return None

    visited.add(visit_key)

    if re.search(r"\babstract\s+(?:partial\s+)?class\b", class_content):
        return None

    if not _is_mobile_base_class(filepath, base_class):
        return None

    if class_name.startswith("Base"):
        return None

    if "[Constructible]" not in class_content and "[Constructable]" not in class_content:
        return None

    constructor_body = _extract_constructible_constructor_body(class_content, class_name)
    if constructor_body is None:
        return None

    base_arguments = _extract_constructible_base_arguments(class_content, class_name)

    variables = _build_ai_metadata_variables(class_content, constructor_body)
    variants, shared_body = _extract_gender_variants(constructor_body)

    data = {
        "class_name": class_name,
        "base_class": base_class,
        "source_file": filepath,
        "shared_equipment_groups": _extract_add_item_groups(shared_body, variables),
    }

    if variants:
        data["variants"] = variants

    data.update(_extract_ai_metadata(base_arguments, constructor_body, variables))

    name_match = re.search(r'DefaultName\s*=>\s*"([^"]+)"', class_content)
    if name_match:
        data["name"] = name_match.group(1)
    else:
        name_assign = _extract_name_assignment(constructor_body)
        if name_assign:
            data["name"] = name_assign

    title_match = re.search(r'base\s*\(\s*"([^"]+)"', class_content)
    if title_match:
        data["title"] = title_match.group(1)

    sound_match = re.search(r"BaseSoundID\s*=\s*(\d+|0x[0-9A-Fa-f]+)", class_content)
    if sound_match:
        data["base_sound_id"] = _parse_int_literal(sound_match.group(1))

    for stat_name, key in [
        ("SetStr", "str"),
        ("SetDex", "dex"),
        ("SetInt", "int"),
        ("SetHits", "hits"),
        ("SetStam", "stam"),
        ("SetMana", "mana"),
        ("SetDamage", "damage"),
    ]:
        stat_match = re.search(
            rf"{stat_name}\s*\(\s*(\d+)\s*(?:,\s*(\d+))?\s*\)",
            class_content,
        )
        if stat_match:
            min_value = int(stat_match.group(1))
            max_value = int(stat_match.group(2)) if stat_match.group(2) else min_value
            data[f"{key}_min"] = min_value
            data[f"{key}_max"] = max_value

    resistances = {}
    for match in re.finditer(
        r"SetResistance\s*\(\s*ResistanceType\.(\w+)\s*,\s*(\d+)\s*(?:,\s*(\d+))?\s*\)",
        class_content,
    ):
        min_value = int(match.group(2))
        max_value = int(match.group(3)) if match.group(3) else min_value
        resistances[match.group(1)] = (min_value, max_value)
    if resistances:
        data["resistances"] = resistances

    damage_types = {}
    for match in re.finditer(
        r"SetDamageType\s*\(\s*ResistanceType\.(\w+)\s*,\s*(\d+)\s*\)",
        class_content,
    ):
        damage_types[match.group(1)] = int(match.group(2))
    if damage_types:
        data["damage_types"] = damage_types

    skills = {}
    for match in re.finditer(
        r"SetSkill\s*\(\s*SkillName\.(\w+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*\)",
        class_content,
    ):
        skills[match.group(1)] = (float(match.group(2)), float(match.group(3)))
    for match in re.finditer(r"Skills\.(\w+)\.Base\s*=\s*([\d.]+)", class_content):
        value = float(match.group(2))
        skills[match.group(1)] = (value, value)
    if skills:
        data["skills"] = skills

    fame_match = re.search(r"Fame\s*=\s*(\d+)", class_content)
    if fame_match:
        data["fame"] = int(fame_match.group(1))

    karma_match = re.search(r"Karma\s*=\s*(-?\d+)", class_content)
    if karma_match:
        data["karma"] = int(karma_match.group(1))

    armor_match = re.search(r"VirtualArmor\s*=\s*(\d+)", class_content)
    if armor_match:
        data["virtual_armor"] = int(armor_match.group(1))

    if re.search(r"Tamable\s*=\s*true", class_content, re.IGNORECASE):
        data["tamable"] = True

    tame_skill_match = re.search(r"MinTameSkill\s*=\s*([\d.]+)", class_content)
    if tame_skill_match:
        data["min_tame_skill"] = float(tame_skill_match.group(1))

    control_slots_match = re.search(r"ControlSlots\s*=\s*(\d+)", class_content)
    if control_slots_match:
        data["control_slots"] = int(control_slots_match.group(1))

    loot_content = _strip_comments(class_content)

    loot_entries = []
    for match in re.finditer(
        r"AddLoot\s*\(\s*LootPack\.(\w+)(?:\s*,\s*(\d+))?\s*\)",
        loot_content,
    ):
        loot_entries.append(
            {
                "pack": match.group(1),
                "count": int(match.group(2)) if match.group(2) else 1,
            }
        )
    if loot_entries:
        data["loot"] = loot_entries

    pack_items = []
    for match in re.finditer(
        r"PackItem\s*\(\s*new\s+(\w+)\s*\(\s*(\d+)?\s*\)\s*\)",
        loot_content,
    ):
        pack_items.append(
            {
                "item": match.group(1),
                "amount": int(match.group(2)) if match.group(2) else 1,
            }
        )
    for match in re.finditer(
        r"PackItem\s*\(\s*Seed\.(?:RandomPeculiarSeed|RandomBonsaiSeed)\s*\([^)]*\)\s*\)",
        loot_content,
    ):
        pack_items.append({"item": "Seed", "amount": 1})

    for match in re.finditer(
        r"PackItem\s*\(\s*Loot\.Random(?:Possible|Necromancy)?Reagent\s*\(\s*\)\s*\)",
        loot_content,
    ):
        pack_items.append({"item": "Reagent", "amount": 1})

    for match in re.finditer(r"PackReg\s*\(\s*(\d+)\s*\)", loot_content):
        pack_items.append({"item": "Reagent", "amount": int(match.group(1))})
    for match in re.finditer(
        r"PackGold\s*\(\s*(\d+)\s*(?:,\s*(\d+))?\s*\)",
        loot_content,
    ):
        min_gold = int(match.group(1))
        max_gold = int(match.group(2)) if match.group(2) else min_gold
        pack_items.append({"item": "Gold", "amount": (min_gold + max_gold) // 2})
    if pack_items:
        data["pack_items"] = pack_items

    sb_infos = []
    for match in re.finditer(
        r"m_SBInfos\.Add\s*\(\s*new\s+(\w+)\s*\(\s*\)\s*\)",
        class_content,
    ):
        sb_infos.append(match.group(1))
    if sb_infos:
        data["sb_infos"] = sb_infos

    appearance_source = shared_body if variants else constructor_body
    appearance_variables = _extract_simple_variables(appearance_source)
    appearance = _extract_appearance_assignments(appearance_source, appearance_variables)
    data.update(appearance)

    if "body" not in data and "body_options" not in data and base_arguments:
        base_parts = _split_top_level_arguments(base_arguments)
        if base_parts:
            body_choices = _parse_body_choices_expression(base_parts[0], variables)
            if body_choices:
                if len(body_choices) == 1:
                    data["body"] = body_choices[0]
                else:
                    data["body_options"] = body_choices

    if not variants and "body" not in data and "body_options" not in data:
        inherited_appearance = _inherit_parent_appearance(filepath, base_class, visited)
        data.update(inherited_appearance)

    missing_ai_keys = {
        key
        for key in ("ai_type", "fight_mode", "range_perception", "range_fight")
        if key not in data
    }
    if missing_ai_keys:
        inherited_ai = _inherit_parent_ai_metadata(
            filepath,
            base_class,
            base_arguments,
            variables,
            set(visited),
        )
        for key in ("ai_type", "fight_mode", "range_perception", "range_fight"):
            if key not in data and key in inherited_ai:
                data[key] = inherited_ai[key]

    if not variants and "body" not in data and "body_options" not in data and "variants" not in data:
        default_variants = _build_base_class_variants(base_class)
        if default_variants:
            data["variants"] = default_variants

    init_outfit_data = _extract_init_outfit_data(
        filepath,
        class_name,
        base_class,
        class_content,
        set(),
    )

    if init_outfit_data["shared_equipment_groups"]:
        data["shared_equipment_groups"].extend(init_outfit_data["shared_equipment_groups"])

    if init_outfit_data["shared_appearance"]:
        data.update(init_outfit_data["shared_appearance"])

    variants_data = data.get("variants")
    if variants_data:
        variants_by_name = {}
        for variant in variants_data:
            variant_name = variant.get("name")
            if isinstance(variant_name, str):
                variants_by_name[variant_name] = variant

        for variant_name, override in init_outfit_data["variant_overrides"].items():
            variant = variants_by_name.get(variant_name)
            if variant is None:
                continue

            variant.setdefault("equipment_groups", [])
            variant["equipment_groups"].extend(override.get("equipment_groups", []))
            variant.setdefault("appearance", {})
            variant["appearance"].update(override.get("appearance", {}))

    return data


def _parse_named_class_from_file(
    filepath: str,
    class_name: str,
    visited: Optional[set[str]] = None,
) -> Optional[dict]:
    if visited is None:
        visited = set()

    class_definition = _find_class_definition(filepath, class_name)
    if class_definition is None:
        return None

    resolved_class_name, base_class, class_content = class_definition
    return _parse_class_definition(filepath, resolved_class_name, base_class, class_content, visited)


def parse_file(filepath: str, visited: Optional[set[str]] = None) -> Optional[dict]:
    """Parse the first mobile class in a C# source file."""
    if visited is None:
        visited = set()

    with open(filepath, "r", encoding="utf-8-sig") as file_handle:
        content = file_handle.read()

    for class_name, base_class, class_content in _extract_class_definitions(content):
        parsed = _parse_class_definition(filepath, class_name, base_class, class_content, set(visited))
        if parsed is not None:
            return parsed

    return None


def parse_directory(source_path: str, category: str, category_path: str) -> list:
    """Parse all C# files in a category directory."""
    full_path = os.path.join(source_path, category_path)
    if not os.path.isdir(full_path):
        print(f"  Warning: directory not found: {full_path}")
        return []

    results = []
    for root, _dirs, files in os.walk(full_path):
        for filename in sorted(files):
            if not filename.endswith(".cs"):
                continue

            filepath = os.path.join(root, filename)
            try:
                with open(filepath, "r", encoding="utf-8-sig") as file_handle:
                    content = file_handle.read()

                relative_path = os.path.relpath(root, full_path)
                subcategory = relative_path if relative_path != "." else ""

                for class_name, base_class, class_content in _extract_class_definitions(content):
                    parsed = _parse_class_definition(filepath, class_name, base_class, class_content, set())
                    if parsed:
                        parsed["category"] = category
                        parsed["subcategory"] = subcategory
                        results.append(parsed)
            except Exception as exception:
                print(f"  Error parsing {filepath}: {exception}")

    return results
