#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

function usage() {
  console.error(
    "Usage: node converters/dfn-to-json.mjs <dfn-root-dir> <output-root-dir> [--allow-unresolved]"
  );
}

function renderProgress(prefix, current, total, currentFile) {
  const safeTotal = total <= 0 ? 1 : total;
  const ratio = Math.min(1, Math.max(0, current / safeTotal));
  const width = 24;
  const filled = Math.round(ratio * width);
  const bar = `${"#".repeat(filled)}${"-".repeat(width - filled)}`;
  const percent = (ratio * 100).toFixed(1).padStart(5, " ");
  const fileText = currentFile ? ` ${currentFile}` : "";
  const line = `\r${prefix} [${bar}] ${current}/${total} ${percent}%${fileText}`;
  process.stdout.write(line);
}

function finishProgress() {
  process.stdout.write("\n");
}

function stripInlineComment(line) {
  const idx = line.indexOf("//");
  if (idx < 0) {
    return line.trim();
  }

  if (idx > 0 && line[idx - 1] === "#") {
    return line.trim();
  }

  return line.slice(0, idx).trim();
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
        current = { section: sectionMatch[1].trim(), entries: [] };
        sections.push(current);
        continue;
      }

      if (line.startsWith("{")) {
        insideBlock = true;
      }

      continue;
    }

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
      current.entries.push([line.slice(0, eqIndex).trim().toLowerCase(), line.slice(eqIndex + 1).trim()]);
      continue;
    }

    const firstSpace = line.indexOf(" ");
    if (firstSpace > 0) {
      const key = line.slice(0, firstSpace).trim().toLowerCase();
      const value = line.slice(firstSpace + 1).trim();
      current.entries.push([key, value]);
      continue;
    }

    current.entries.push(["__line__", line]);
  }

  return sections;
}

function collectFilesByExt(rootDir, ext) {
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

      if (entry.isFile() && entry.name.toLowerCase().endsWith(ext)) {
        results.push(fullPath);
      }
    }
  }

  results.sort((a, b) => a.localeCompare(b));
  return results;
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

function firstValue(entryMap, key) {
  const values = entryMap.get(key);
  return values && values.length > 0 ? values[0] : undefined;
}

function allValues(entryMap, key) {
  const values = entryMap.get(key);
  return values ?? [];
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

function sectionToItemTemplate(section, category) {
  const entryMap = new Map();
  for (const [key, value] of section.entries) {
    if (key === "__line__") {
      continue;
    }

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

function normalizeToken(token) {
  return String(token ?? "").trim();
}

function normalizeHexToken(token) {
  const raw = normalizeToken(token).toLowerCase();
  if (!raw) {
    return "";
  }

  const with0x = raw.startsWith("0x") ? raw : /^([0-9a-f]+)$/i.test(raw) ? `0x${raw}` : raw;
  if (!/^0x[0-9a-f]+$/i.test(with0x)) {
    return "";
  }

  return `0x${with0x.slice(2).replace(/^0+/, "") || "0"}`;
}

function buildItemResolver(itemsDfnDir) {
  const dfnFiles = collectFilesByExt(itemsDfnDir, ".dfn");
  const bySection = new Map();
  const byGraphic = new Map();
  const bySectionMeta = new Map();
  const itemLists = new Map();
  const listObjects = new Map();

  for (const filePath of dfnFiles) {
    const sections = parseDfnSections(fs.readFileSync(filePath, "utf8"));
    for (const section of sections) {
      const sectionId = section.section;
      const sectionKey = sectionId.toLowerCase();
      const lowerSection = sectionKey;

      if (lowerSection.startsWith("itemlist ")) {
        const listId = lowerSection.slice("itemlist ".length).trim();
        const entries = [];
        for (const [key, value] of section.entries) {
          if (key === "__line__") {
            const parsed = parseLootLine(value);
            if (parsed) {
              entries.push(parsed);
            }
          }
        }

        if (entries.length > 0) {
          itemLists.set(listId, entries);
        }

        continue;
      }

      if (lowerSection.startsWith("listobject")) {
        for (const [key, value] of section.entries) {
          if (key === "itemlist") {
            listObjects.set(lowerSection, String(value).trim().toLowerCase());
            break;
          }
        }

        continue;
      }

      if (!bySection.has(sectionKey)) {
        bySection.set(sectionKey, sectionId);
      }

      let layerValue;
      for (const [key, value] of section.entries) {
        if (key === "layer") {
          const parsedLayer = toIntFlexible(value);
          if (Number.isFinite(parsedLayer) && parsedLayer >= 0) {
            layerValue = parsedLayer;
          }
        }
      }

      bySectionMeta.set(sectionKey, { layerValue });

      for (const [key, value] of section.entries) {
        if (key !== "id") {
          continue;
        }

        const normalizedHex = normalizeHexToken(value);
        if (!normalizedHex) {
          continue;
        }

        const existing = byGraphic.get(normalizedHex);
        if (!existing) {
          byGraphic.set(normalizedHex, sectionId);
          continue;
        }

        const existingIsBase = existing.toLowerCase().startsWith("base_");
        const currentIsBase = sectionId.toLowerCase().startsWith("base_");
        if (existingIsBase && !currentIsBase) {
          byGraphic.set(normalizedHex, sectionId);
        }
      }
    }
  }

  return {
    resolve(token) {
      const normalized = normalizeToken(token);
      if (!normalized) {
        return null;
      }

      const sanitized = normalized.replace(/^(?:item|id)\s*=\s*/i, "").trim();
      const sectionHit = bySection.get(sanitized.toLowerCase());
      if (sectionHit) {
        return sectionHit;
      }

      const asHex = normalizeHexToken(sanitized);
      if (asHex) {
        const graphicHit = byGraphic.get(asHex);
        if (graphicHit) {
          return graphicHit;
        }
      }

      return null;
    },
    resolveMeta(token) {
      const resolved = this.resolve(token);
      if (!resolved) {
        return null;
      }

      const meta = bySectionMeta.get(resolved.toLowerCase()) ?? {};
      return {
        itemTemplateId: resolved,
        layer: mapLayerByteToName(meta.layerValue)
      };
    },
    resolveListObject(token) {
      const normalized = normalizeToken(token).toLowerCase();
      if (!normalized.startsWith("listobject")) {
        return null;
      }

      const listId = listObjects.get(normalized);
      if (!listId) {
        return null;
      }

      const entries = itemLists.get(listId);
      if (!entries || entries.length === 0) {
        return null;
      }

      const items = [];
      for (const entry of entries) {
        const resolved = this.resolve(entry.value);
        if (!resolved) {
          continue;
        }

        items.push({
          itemTemplateId: resolved,
          weight: entry.weight
        });
      }

      if (items.length === 0) {
        return null;
      }

      return {
        name: normalized,
        layer: "Invalid",
        spawnChance: 1.0,
        items
      };
    }
  };
}

function parseLootLine(rawLine) {
  const line = stripInlineComment(rawLine);
  if (!line) {
    return null;
  }

  const pipe = line.indexOf("|");
  if (pipe >= 0) {
    const left = line.slice(0, pipe).trim();
    const right = line.slice(pipe + 1).trim();
    const weight = Number.parseInt(left, 10);
    return {
      weight: Number.isNaN(weight) ? 1 : weight,
      value: right
    };
  }

  return {
    weight: 1,
    value: line.trim()
  };
}

function isBlankToken(value) {
  return value.trim().toLowerCase() === "blank";
}

function convertLootDfnToTemplates(content, resolver, category) {
  const sections = parseDfnSections(content);
  const output = [];
  let resolved = 0;
  let unresolved = 0;
  const unresolvedEntries = [];

  for (const section of sections) {
    if (!section.section.toLowerCase().startsWith("lootlist ")) {
      continue;
    }

    const lootId = section.section.slice("lootlist ".length).trim();
    const template = {
      type: "loot",
      id: lootId,
      name: lootId,
      category,
      description: `Converted from UOX3 LOOTLIST ${lootId}`,
      noDropWeight: 0,
      entries: []
    };

    for (const [key, value] of section.entries) {
      if (key !== "__line__") {
        continue;
      }

      const parsed = parseLootLine(value);
      if (!parsed) {
        continue;
      }

      if (isBlankToken(parsed.value)) {
        template.noDropWeight += parsed.weight;
        continue;
      }

      const resolvedItem = resolver.resolve(parsed.value);
      if (resolvedItem) {
        template.entries.push({
          weight: parsed.weight,
          itemTemplateId: resolvedItem,
          amount: 1
        });
        resolved++;
      } else {
        unresolvedEntries.push({ lootId, value: parsed.value });
        unresolved++;
      }
    }

    output.push(template);
  }

  return { templates: output, resolved, unresolved, unresolvedEntries };
}

function parseRangeAverage(value, fallback = 0) {
  if (!value) {
    return fallback;
  }

  const tokens = String(value)
    .split(/[\s,]+/)
    .map((x) => x.trim())
    .filter(Boolean)
    .map((x) => toIntFlexible(x))
    .filter((x) => Number.isFinite(x));

  if (tokens.length === 0) {
    return fallback;
  }

  if (tokens.length === 1) {
    return tokens[0];
  }

  return Math.floor((tokens[0] + tokens[1]) / 2);
}

function parseRangePair(value, defaultMin = 0, defaultMax = 0) {
  if (!value) {
    return [defaultMin, defaultMax];
  }

  const tokens = String(value)
    .split(/[\s,]+/)
    .map((x) => x.trim())
    .filter(Boolean)
    .map((x) => toIntFlexible(x))
    .filter((x) => Number.isFinite(x));

  if (tokens.length === 0) {
    return [defaultMin, defaultMax];
  }

  if (tokens.length === 1) {
    return [tokens[0], tokens[0]];
  }

  return [tokens[0], tokens[1]];
}

function parseUoxName(rawName, fallback) {
  const value = String(rawName ?? "").trim();
  if (!value) {
    return fallback;
  }

  if (value.startsWith("#//")) {
    return value.slice(3).trim() || fallback;
  }

  if (value.startsWith("#")) {
    return value.slice(1).trim() || fallback;
  }

  return value;
}

function mapUoxFlagToNotoriety(flagValue) {
  const flag = String(flagValue ?? "").trim().toLowerCase();
  switch (flag) {
    case "evil":
      return "Murdered";
    case "neutral":
      return "Enemy";
    case "good":
      return "Innocent";
    case "criminal":
      return "Criminal";
    case "friend":
      return "Friend";
    case "animal":
      return "Animal";
    case "invulnerable":
      return "Invulnerable";
    default:
      return "Innocent";
  }
}

function mapUoxSkillName(key) {
  const lower = key.toLowerCase();
  const overrides = {
    magicresistance: "magicResistance",
    evaluatingintel: "evaluatingIntel",
    animallore: "animalLore",
    detecthidden: "detectHidden",
    itemid: "itemId",
    tasteid: "tasteId",
    spiritsspeak: "spiritsSpeak",
    removetrap: "removeTrap",
    macefighting: "maceFighting"
  };

  return overrides[lower] ?? lower;
}

function mapUoxSoundKey(key) {
  const value = key.toUpperCase();
  switch (value) {
    case "SOUND_STARTATTACK":
      return "StartAttack";
    case "SOUND_IDLE":
      return "Idle";
    case "SOUND_ATTACK":
      return "Attack";
    case "SOUND_DEFEND":
      return "Defend";
    case "SOUND_DIE":
      return "Die";
    default:
      return null;
  }
}

function mapLayerByteToName(value) {
  const map = {
    0: "Invalid",
    1: "OneHanded",
    2: "TwoHanded",
    3: "Shoes",
    4: "Pants",
    5: "Shirt",
    6: "Helm",
    7: "Gloves",
    8: "Ring",
    9: "Talisman",
    10: "Neck",
    11: "Hair",
    12: "Waist",
    13: "InnerTorso",
    14: "Bracelet",
    16: "FacialHair",
    17: "MiddleTorso",
    18: "Earrings",
    19: "Arms",
    20: "Cloak",
    21: "Backpack",
    22: "OuterTorso",
    23: "OuterLegs",
    24: "InnerLegs",
    25: "Mount",
    26: "ShopBuy",
    27: "ShopResale",
    28: "ShopSell",
    29: "Bank"
  };

  return map[value] ?? "Invalid";
}

function buildEntryMap(section) {
  const entryMap = new Map();
  const flags = new Set();
  for (const [key, value] of section.entries) {
    if (key === "__line__") {
      const normalized = String(value ?? "").trim().toLowerCase();
      if (normalized) {
        flags.add(normalized);
      }

      continue;
    }

    const bucket = entryMap.get(key);
    if (bucket) {
      bucket.push(value);
    } else {
      entryMap.set(key, [value]);
    }
  }

  return { entryMap, flags };
}

function resolveBaseMobile(entryMap) {
  const rawGet = firstValue(entryMap, "get");
  if (!rawGet) {
    return undefined;
  }

  const token = rawGet.split(/[\s,]+/).map((x) => x.trim()).filter(Boolean)[0];
  if (!token) {
    return undefined;
  }

  if (/^0x/i.test(token)) {
    return undefined;
  }

  return token;
}

function sectionToMobileTemplate(section, category, itemResolver) {
  const { entryMap, flags } = buildEntryMap(section);
  const name = parseUoxName(firstValue(entryMap, "name"), section.section);
  const [minDamage, maxDamage] = parseRangePair(firstValue(entryMap, "damage"), 0, 0);
  const [goldMin, goldMax] = parseRangePair(firstValue(entryMap, "gold"), 0, 0);
  const lootTables = allValues(entryMap, "loot")
    .map((x) => String(x).split(",")[0].trim())
    .filter(Boolean);

  const sounds = {};
  for (const [key, values] of entryMap.entries()) {
    const mapped = mapUoxSoundKey(key);
    if (!mapped || !values || values.length === 0) {
      continue;
    }

    sounds[mapped] = toIntFlexible(values[0]);
  }

  const nonSkillKeys = new Set([
    "name",
    "id",
    "direction",
    "backpack",
    "packitem",
    "loot",
    "str",
    "dex",
    "int",
    "hpmax",
    "hits",
    "manamax",
    "mana",
    "stam",
    "stammax",
    "karma",
    "fame",
    "damage",
    "def",
    "npcwander",
    "fx1",
    "fy1",
    "fz1",
    "fx2",
    "fy2",
    "fz2",
    "npcai",
    "brain",
    "race",
    "flag",
    "gold",
    "totame",
    "toprov",
    "topeace",
    "controlslots",
    "spattack",
    "spadelay",
    "canrun",
    "fleeat",
    "origin",
    "color",
    "skin",
    "haircolor",
    "hairstyle",
    "saycolor",
    "emotecolor",
    "script",
    "get",
    "equipitem"
  ]);

  const skills = {};
  for (const [key, values] of entryMap.entries()) {
    if (nonSkillKeys.has(key) || key.startsWith("sound_")) {
      continue;
    }

    if (!values || values.length === 0) {
      continue;
    }

    const parsed = parseRangeAverage(values[0], Number.NaN);
    if (!Number.isFinite(parsed)) {
      continue;
    }

    skills[mapUoxSkillName(key)] = parsed;
  }

  const template = {
    type: "mobile",
    id: section.section,
    name,
    category,
    description: name,
    tags: [],
    body: toIntFlexible(firstValue(entryMap, "id")),
    skinHue: toIntFlexible(firstValue(entryMap, "skin") ?? firstValue(entryMap, "color")),
    hairHue: toIntFlexible(firstValue(entryMap, "haircolor")),
    hairStyle: toIntFlexible(firstValue(entryMap, "hairstyle")),
    strength: parseRangeAverage(firstValue(entryMap, "str"), 50),
    dexterity: parseRangeAverage(firstValue(entryMap, "dex"), 50),
    intelligence: parseRangeAverage(firstValue(entryMap, "int"), 50),
    hits: parseRangeAverage(firstValue(entryMap, "hpmax") ?? firstValue(entryMap, "hits"), 100),
    maxHits: parseRangeAverage(firstValue(entryMap, "hpmax"), 0),
    mana: parseRangeAverage(firstValue(entryMap, "manamax") ?? firstValue(entryMap, "mana"), 100),
    stamina: parseRangeAverage(firstValue(entryMap, "stammax") ?? firstValue(entryMap, "stam"), 100),
    minDamage,
    maxDamage,
    armorRating: toIntFlexible(firstValue(entryMap, "def")),
    fame: toIntFlexible(firstValue(entryMap, "fame")),
    karma: toIntFlexible(firstValue(entryMap, "karma")),
    notoriety: mapUoxFlagToNotoriety(firstValue(entryMap, "flag")),
    brain: firstValue(entryMap, "brain") ?? (firstValue(entryMap, "npcai") ? `uox_npcai_${firstValue(entryMap, "npcai")}` : "None"),
    sounds,
    goldDrop: goldMax > goldMin ? `dice(1d${goldMax - goldMin + 1}+${goldMin - 1})` : String(goldMin),
    lootTables,
    skills,
    tamingDifficulty: parseRangeAverage(firstValue(entryMap, "totame"), 0),
    provocationDifficulty: parseRangeAverage(firstValue(entryMap, "toprov"), 0),
    pacificationDifficulty: parseRangeAverage(firstValue(entryMap, "topeace"), 0),
    controlSlots: toIntFlexible(firstValue(entryMap, "controlslots")),
    canRun: flags.has("runs") || toBoolFlexible(firstValue(entryMap, "canrun")),
    fleesAtHitsPercent: toIntFlexible(firstValue(entryMap, "fleeat") ?? "-1"),
    spellAttackType: toIntFlexible(firstValue(entryMap, "spattack")),
    spellAttackDelay: toIntFlexible(firstValue(entryMap, "spadelay")),
    fixedEquipment: [],
    randomEquipment: []
  };

  const equipItems = allValues(entryMap, "equipitem").map((x) => String(x).trim()).filter(Boolean);
  for (const equipToken of equipItems) {
    const listPool = itemResolver.resolveListObject(equipToken);
    if (listPool) {
      template.randomEquipment.push(listPool);
      continue;
    }

    const resolved = itemResolver.resolveMeta(equipToken);
    if (!resolved) {
      continue;
    }

    template.fixedEquipment.push({
      itemTemplateId: resolved.itemTemplateId,
      layer: resolved.layer
    });
  }

  const baseMobile = resolveBaseMobile(entryMap);
  if (baseMobile) {
    template.base_mobile = baseMobile;
  }

  const origin = firstValue(entryMap, "origin");
  if (origin) {
    template.tags.push(`origin:${String(origin).toLowerCase()}`);
  }

  const race = firstValue(entryMap, "race");
  if (race) {
    template.tags.push(`race:${race}`);
  }

  return template;
}

function validateMobileTemplate(template) {
  const errors = [];
  if (!template.id) {
    errors.push("missing id");
  }

  if (!template.name) {
    errors.push("missing name");
  }

  if ((!Number.isFinite(template.body) || template.body <= 0) && !template.base_mobile) {
    errors.push("missing/invalid body");
  }

  if (template.minDamage > template.maxDamage) {
    errors.push("minDamage > maxDamage");
  }

  if (template.strength < 0 || template.dexterity < 0 || template.intelligence < 0 || template.hits < 0) {
    errors.push("negative core stats");
  }

  return errors;
}

function isCreatureSection(section) {
  const keys = new Set();
  const flags = new Set();
  for (const [key, value] of section.entries) {
    if (key === "__line__") {
      const normalized = String(value ?? "").trim().toLowerCase();
      if (normalized) {
        flags.add(normalized);
      }

      continue;
    }

    keys.add(key);
  }

  const hasIdentity = keys.has("id") || keys.has("get");
  const hasStatSignal = keys.has("str") || keys.has("dex") || keys.has("int") || keys.has("damage");
  const hasNpcMarkers =
    keys.has("npcai") ||
    keys.has("brain") ||
    keys.has("npcwander") ||
    keys.has("toprov") ||
    keys.has("topeace") ||
    keys.has("totame") ||
    keys.has("controlslots") ||
    keys.has("race") ||
    keys.has("fleeat") ||
    keys.has("spattack") ||
    keys.has("spadelay") ||
    keys.has("flag") ||
    keys.has("karma") ||
    keys.has("fame") ||
    keys.has("packitem") ||
    flags.has("runs") ||
    flags.has("backpack");

  return hasIdentity && hasStatSignal && hasNpcMarkers;
}

function isLootSection(section) {
  return section.section.toLowerCase().startsWith("lootlist ");
}

function isItemSection(section) {
  if (isLootSection(section) || isCreatureSection(section)) {
    return false;
  }

  for (const [key] of section.entries) {
    if (key === "id") {
      return true;
    }
  }

  return false;
}

function convertItems(dfnRootDir, itemsOutputDir) {
  const dfnFiles = collectFilesByExt(dfnRootDir, ".dfn");
  let convertedFiles = 0;
  let totalSections = 0;
  const total = dfnFiles.length;

  for (let i = 0; i < dfnFiles.length; i++) {
    const dfnFile = dfnFiles[i];
    const content = fs.readFileSync(dfnFile, "utf8");
    const sections = parseDfnSections(content).filter((section) => isItemSection(section));
    const category = path.basename(path.dirname(dfnFile)) || "items";
    const templates = sections.map((section) => sectionToItemTemplate(section, category));

    if (templates.length > 0) {
      const relative = path.relative(dfnRootDir, dfnFile);
      const outputFile = path.join(itemsOutputDir, relative).replace(/\.dfn$/i, ".json");
      fs.mkdirSync(path.dirname(outputFile), { recursive: true });
      fs.writeFileSync(outputFile, `${JSON.stringify(templates, null, 2)}\n`, "utf8");
      convertedFiles++;
      totalSections += sections.length;
    }

    renderProgress("Items", i + 1, total, path.relative(dfnRootDir, dfnFile));
  }

  if (total > 0) {
    finishProgress();
  }

  return { convertedFiles, totalSections };
}

function convertCreatures(dfnRootDir, mobilesOutputDir, itemResolver) {
  const dfnFiles = collectFilesByExt(dfnRootDir, ".dfn");
  const total = dfnFiles.length;
  let convertedFiles = 0;
  let totalTemplates = 0;
  let totalSkipped = 0;
  let totalValidationErrors = 0;
  let totalIgnored = 0;

  for (let i = 0; i < dfnFiles.length; i++) {
    const dfnFile = dfnFiles[i];
    const content = fs.readFileSync(dfnFile, "utf8");
    const sections = parseDfnSections(content);
    const category = path.basename(path.dirname(dfnFile)) || "npc";

    const templates = [];
    for (const section of sections) {
      if (!isCreatureSection(section)) {
        totalIgnored++;
        continue;
      }

      const template = sectionToMobileTemplate(section, category, itemResolver);
      const errors = validateMobileTemplate(template);
      if (errors.length > 0) {
        totalValidationErrors += errors.length;
        totalSkipped++;
        continue;
      }

      templates.push(template);
    }

    if (templates.length > 0) {
      const relative = path.relative(dfnRootDir, dfnFile);
      const outputFile = path.join(mobilesOutputDir, relative).replace(/\.dfn$/i, ".json");
      fs.mkdirSync(path.dirname(outputFile), { recursive: true });
      fs.writeFileSync(outputFile, `${JSON.stringify(templates, null, 2)}\n`, "utf8");
      convertedFiles++;
      totalTemplates += templates.length;
    }

    renderProgress("Creatures", i + 1, total, path.relative(dfnRootDir, dfnFile));
  }

  if (total > 0) {
    finishProgress();
  }

  return { convertedFiles, totalTemplates, totalSkipped, totalValidationErrors, totalIgnored };
}

function convertLoots(lootsInputDir, itemsInputDir, lootsOutputDir, allowUnresolved) {
  const resolver = buildItemResolver(itemsInputDir);
  const dfnFiles = collectFilesByExt(lootsInputDir, ".dfn");
  const total = dfnFiles.length;

  let totalFiles = 0;
  let totalTemplates = 0;
  let totalResolved = 0;
  let totalUnresolved = 0;

  for (let i = 0; i < dfnFiles.length; i++) {
    const lootFile = dfnFiles[i];
    const content = fs.readFileSync(lootFile, "utf8");
    const category = path.basename(path.dirname(lootFile)) || "loot";
    const result = convertLootDfnToTemplates(content, resolver, category);

    if (!allowUnresolved && result.unresolved > 0) {
      const sample = result.unresolvedEntries
        .slice(0, 10)
        .map((entry) => `${entry.lootId}:${entry.value}`)
        .join(", ");
      throw new Error(`Unresolved loot entries in ${lootFile}: ${result.unresolved}. Examples: ${sample}`);
    }

    if (result.templates.length > 0) {
      const relative = path.relative(lootsInputDir, lootFile);
      const outputFile = path.join(lootsOutputDir, relative).replace(/\.dfn$/i, ".json");
      fs.mkdirSync(path.dirname(outputFile), { recursive: true });
      fs.writeFileSync(outputFile, `${JSON.stringify(result.templates, null, 2)}\n`, "utf8");

      totalFiles++;
      totalTemplates += result.templates.length;
      totalResolved += result.resolved;
      totalUnresolved += result.unresolved;
    }

    renderProgress("Loots", i + 1, total, path.relative(lootsInputDir, lootFile));
  }

  if (total > 0) {
    finishProgress();
  }

  return {
    totalFiles,
    totalTemplates,
    totalResolved,
    totalUnresolved
  };
}

function main() {
  const argv = process.argv.slice(2);
  if (argv.length < 2) {
    usage();
    process.exit(1);
  }

  const allowUnresolved = argv.includes("--allow-unresolved");
  const positional = argv.filter((x) => x !== "--allow-unresolved");
  if (positional.length < 2) {
    usage();
    process.exit(1);
  }

  const dfnRootDir = path.resolve(positional[0]);
  const outputRootDir = path.resolve(positional[1]);
  const itemsInputDir = dfnRootDir;
  const lootsInputDir = dfnRootDir;
  const itemsOutputDir = path.join(outputRootDir, "items");
  const mobilesOutputDir = path.join(outputRootDir, "mobiles");
  const lootsOutputDir = path.join(outputRootDir, "loots");

  const itemResolver = buildItemResolver(itemsInputDir);

  console.log("[1/3] Converting item DFN files...");
  const itemsStats = convertItems(dfnRootDir, itemsOutputDir);
  console.log(
    `Converted ${itemsStats.convertedFiles} item DFN files (${itemsStats.totalSections} sections) to ${itemsOutputDir}`
  );

  console.log("[2/3] Converting creature DFN files...");
  const creatureStats = convertCreatures(dfnRootDir, mobilesOutputDir, itemResolver);
  console.log(
    `Converted ${creatureStats.convertedFiles} creature DFN files (${creatureStats.totalTemplates} templates) to ${mobilesOutputDir}. Skipped=${creatureStats.totalSkipped}, ValidationErrors=${creatureStats.totalValidationErrors}, Ignored=${creatureStats.totalIgnored}`
  );

  console.log("[3/3] Converting loot DFN files (using item DFN resolver)...");
  const lootStats = convertLoots(lootsInputDir, itemsInputDir, lootsOutputDir, allowUnresolved);
  console.log(
    `Converted ${lootStats.totalFiles} loot dfn files (${lootStats.totalTemplates} templates). Resolved=${lootStats.totalResolved}, Unresolved=${lootStats.totalUnresolved}`
  );

  console.log(`Done. Output root: ${outputRootDir}`);
}

main();
