#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

function usage() {
  console.error(
    "Usage: node converters/dfn-items-to-json.mjs <input.{dfn|dir}> <output.{json|dir}> [--category <category>]"
  );
}

function stripInlineComment(line) {
  const idx = line.indexOf("//");
  return idx >= 0 ? line.slice(0, idx).trim() : line.trim();
}

function toIntFlexible(value) {
  if (value == null) {
    return 0;
  }

  const trimmed = String(value).trim();
  if (!trimmed) {
    return 0;
  }

  if (/^[-+]?0x[0-9a-f]+$/i.test(trimmed)) {
    return Number.parseInt(trimmed, 16);
  }

  const parsed = Number.parseInt(trimmed, 10);
  return Number.isNaN(parsed) ? 0 : parsed;
}

function toBoolFlexible(value) {
  if (value == null) {
    return false;
  }

  const text = String(value).trim().toLowerCase();
  if (!text) {
    return false;
  }

  if (text === "true" || text === "yes") {
    return true;
  }

  if (text === "false" || text === "no") {
    return false;
  }

  return toIntFlexible(text) > 0;
}

function normalizeSectionId(sectionName) {
  return sectionName.trim();
}

function parseDfnSections(content) {
  const lines = content.split(/\r?\n/);
  const sections = [];
  let current = null;
  let insideBlock = false;

  for (const rawLine of lines) {
    const line = stripInlineComment(rawLine);
    if (!line) {
      continue;
    }

    if (!insideBlock) {
      const sectionMatch = line.match(/^\[(.+)\]$/);
      if (sectionMatch) {
        current = {
          section: normalizeSectionId(sectionMatch[1]),
          entries: []
        };
        sections.push(current);
        continue;
      }

      if (line.startsWith("{")) {
        insideBlock = true;
        continue;
      }
    } else {
      if (line.startsWith("}")) {
        insideBlock = false;
        current = null;
        continue;
      }

      if (!current) {
        continue;
      }

      const eqIndex = line.indexOf("=");
      if (eqIndex >= 0) {
        const key = line.slice(0, eqIndex).trim().toLowerCase();
        const value = line.slice(eqIndex + 1).trim();
        current.entries.push([key, value]);
        continue;
      }

      const firstSpace = line.indexOf(" ");
      if (firstSpace > 0) {
        const key = line.slice(0, firstSpace).trim().toLowerCase();
        const value = line.slice(firstSpace + 1).trim();
        current.entries.push([key, value]);
      } else {
        current.entries.push([line.trim().toLowerCase(), "1"]);
      }
    }
  }

  return sections;
}

function firstValue(entryMap, key) {
  const values = entryMap.get(key);
  return values && values.length > 0 ? values[0] : undefined;
}

function allValues(entryMap, key) {
  const values = entryMap.get(key);
  return values ?? [];
}

function getBaseItemValue(entryMap) {
  const rawGet = firstValue(entryMap, "get");
  if (!rawGet) {
    return undefined;
  }

  const tokens = rawGet
    .split(/\s+/)
    .map((x) => x.trim())
    .filter(Boolean);

  if (tokens.length === 1) {
    return tokens[0];
  }

  return undefined;
}

function mapValueToGoldValueSpec(valueText) {
  if (!valueText) {
    return "0";
  }

  const tokens = valueText.split(/\s+/).filter(Boolean);
  if (tokens.length === 1) {
    return String(toIntFlexible(tokens[0]));
  }

  if (tokens.length >= 2) {
    const min = toIntFlexible(tokens[0]);
    const variance = toIntFlexible(tokens[1]);
    if (variance <= 0) {
      return String(min);
    }

    return `dice(1d${variance + 1}+${min - 1})`;
  }

  return "0";
}

function deriveCategory(inputPath, explicitCategory) {
  if (explicitCategory) {
    return explicitCategory;
  }

  const parent = path.basename(path.dirname(inputPath));
  return parent || "items";
}

function sectionToItemTemplate(section, category) {
  const entryMap = new Map();
  for (const [key, value] of section.entries) {
    const bucket = entryMap.get(key);
    if (bucket) {
      bucket.push(value);
    } else {
      entryMap.set(key, [value]);
    }
  }

  const template = {
    type: "item",
    id: section.section,
    name: firstValue(entryMap, "name") ?? section.section,
    category,
    description: firstValue(entryMap, "name") ?? section.section,
    container: [],
    dyeable: toBoolFlexible(firstValue(entryMap, "dyeable")),
    goldValue: mapValueToGoldValueSpec(firstValue(entryMap, "value")),
    hue: String(toIntFlexible(firstValue(entryMap, "color"))),
    isMovable: toBoolFlexible(firstValue(entryMap, "movable")),
    itemId: firstValue(entryMap, "id") ?? "0x0000",
    lootType: toBoolFlexible(firstValue(entryMap, "newbie")) ? "Newbied" : "Regular",
    scriptId: (firstValue(entryMap, "script") ?? "").split(/\s+/)[0],
    stackable: toBoolFlexible(firstValue(entryMap, "pileable")),
    tags: [],
    weight: toIntFlexible(firstValue(entryMap, "weight")),
    weightMax: toIntFlexible(firstValue(entryMap, "weightmax")),
    maxItems: toIntFlexible(firstValue(entryMap, "maxitems")),
    lowDamage: toIntFlexible(firstValue(entryMap, "lodamage")),
    highDamage: toIntFlexible(firstValue(entryMap, "hidamage")),
    defense: toIntFlexible(firstValue(entryMap, "def")),
    hitPoints: toIntFlexible(firstValue(entryMap, "hp")),
    speed: toIntFlexible(firstValue(entryMap, "spd")),
    strength: toIntFlexible(firstValue(entryMap, "str")),
    strengthAdd: toIntFlexible(firstValue(entryMap, "stradd")),
    dexterity: toIntFlexible(firstValue(entryMap, "dex")),
    dexterityAdd: toIntFlexible(firstValue(entryMap, "dexadd")),
    intelligence: toIntFlexible(firstValue(entryMap, "int")),
    intelligenceAdd: toIntFlexible(firstValue(entryMap, "intadd")),
    ammo: toIntFlexible(firstValue(entryMap, "ammo")),
    ammoFx: toIntFlexible(firstValue(entryMap, "ammofx")),
    maxRange: toIntFlexible(firstValue(entryMap, "maxrange")),
    baseRange: toIntFlexible(firstValue(entryMap, "baserange"))
  };

  const baseItem = getBaseItemValue(entryMap);
  if (baseItem) {
    template.base_item = baseItem;
  }

  const origin = firstValue(entryMap, "origin");
  if (origin) {
    template.tags.push(`origin:${origin.toLowerCase()}`);
  }

  const colorList = firstValue(entryMap, "colorlist");
  if (colorList) {
    template.tags.push(`colorlist:${colorList}`);
  }

  for (const scriptValue of allValues(entryMap, "script").slice(1)) {
    const normalized = scriptValue.split(/\s+/)[0];
    if (normalized) {
      template.tags.push(`script:${normalized}`);
    }
  }

  return template;
}

function parseArgs(argv) {
  const positional = [];
  let category;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--category") {
      category = argv[i + 1];
      i++;
      continue;
    }

    positional.push(arg);
  }

  if (positional.length < 2) {
    return null;
  }

  return {
    inputPath: positional[0],
    outputPath: positional[1],
    category
  };
}

function collectDfnFiles(rootDir) {
  const results = [];
  const stack = [rootDir];

  while (stack.length > 0) {
    const current = stack.pop();
    const entries = fs.readdirSync(current, { withFileTypes: true });

    for (const entry of entries) {
      const fullPath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      if (entry.isFile() && entry.name.toLowerCase().endsWith(".dfn")) {
        results.push(fullPath);
      }
    }
  }

  results.sort((a, b) => a.localeCompare(b));
  return results;
}

function convertSingleFile(inputPath, outputPath, categoryOverride) {
  const category = deriveCategory(inputPath, categoryOverride);
  const content = fs.readFileSync(inputPath, "utf8");
  const sections = parseDfnSections(content);
  const templates = sections.map((section) => sectionToItemTemplate(section, category));

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(templates, null, 2)}\n`, "utf8");

  return sections.length;
}

function main() {
  const parsed = parseArgs(process.argv.slice(2));
  if (!parsed) {
    usage();
    process.exit(1);
  }

  const inputPath = path.resolve(parsed.inputPath);
  const outputPath = path.resolve(parsed.outputPath);
  const inputStat = fs.statSync(inputPath);

  if (inputStat.isFile()) {
    const sections = convertSingleFile(inputPath, outputPath, parsed.category);
    console.log(`Converted ${sections} DFN sections to ${outputPath}`);
    return;
  }

  if (!inputStat.isDirectory()) {
    throw new Error(`Unsupported input path: ${inputPath}`);
  }

  const dfnFiles = collectDfnFiles(inputPath);
  if (dfnFiles.length === 0) {
    console.log(`No .dfn files found under ${inputPath}`);
    return;
  }

  let totalSections = 0;
  let convertedFiles = 0;

  for (const dfnFile of dfnFiles) {
    const relative = path.relative(inputPath, dfnFile);
    const outputFile = path.join(outputPath, relative).replace(/\.dfn$/i, ".json");
    totalSections += convertSingleFile(dfnFile, outputFile, parsed.category);
    convertedFiles++;
  }

  console.log(
    `Converted ${convertedFiles} DFN files (${totalSections} sections) from ${inputPath} to ${outputPath}`
  );
}

main();
