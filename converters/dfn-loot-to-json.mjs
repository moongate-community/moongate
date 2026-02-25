#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

function usage() {
  console.error(
    "Usage: node converters/dfn-loot-to-json.mjs <loot.{dfn|dir}> <output.{json|dir}> --items-dfn <items-dfn-dir> [--allow-unresolved]"
  );
}

function stripInlineComment(line) {
  const idx = line.indexOf("//");
  return idx >= 0 ? line.slice(0, idx).trim() : line.trim();
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

    current.entries.push(["__line__", line]);
  }

  return sections;
}

function collectFilesByExt(rootDir, ext) {
  const out = [];
  const stack = [rootDir];

  while (stack.length > 0) {
    const dir = stack.pop();
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      if (entry.isFile() && entry.name.toLowerCase().endsWith(ext)) {
        out.push(fullPath);
      }
    }
  }

  out.sort((a, b) => a.localeCompare(b));
  return out;
}

function parseArgs(argv) {
  const positional = [];
  let itemsDfnDir = "";
  let allowUnresolved = false;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === "--items-dfn") {
      itemsDfnDir = argv[i + 1] ?? "";
      i++;
      continue;
    }
    if (arg === "--allow-unresolved") {
      allowUnresolved = true;
      continue;
    }

    positional.push(arg);
  }

  if (positional.length < 2 || !itemsDfnDir) {
    return null;
  }

  return {
    lootInput: positional[0],
    outputPath: positional[1],
    itemsDfnDir,
    allowUnresolved
  };
}

function buildItemResolver(itemsDfnDir) {
  const dfnFiles = collectFilesByExt(itemsDfnDir, ".dfn");
  const bySection = new Map();
  const byGraphic = new Map();

  for (const filePath of dfnFiles) {
    const sections = parseDfnSections(fs.readFileSync(filePath, "utf8"));
    for (const section of sections) {
      const sectionId = section.section;
      const sectionKey = sectionId.toLowerCase();
      if (!bySection.has(sectionKey)) {
        bySection.set(sectionKey, sectionId);
      }

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

function convertSingleLootFile(inputFile, outputFile, resolver, allowUnresolved) {
  const content = fs.readFileSync(inputFile, "utf8");
  const category = path.basename(path.dirname(inputFile)) || "loot";
  const result = convertLootDfnToTemplates(content, resolver, category);

  if (!allowUnresolved && result.unresolved > 0) {
    const sample = result.unresolvedEntries
      .slice(0, 10)
      .map((entry) => `${entry.lootId}:${entry.value}`)
      .join(", ");
    throw new Error(
      `Unresolved loot entries in ${inputFile}: ${result.unresolved}. Examples: ${sample}`
    );
  }

  fs.mkdirSync(path.dirname(outputFile), { recursive: true });
  fs.writeFileSync(outputFile, `${JSON.stringify(result.templates, null, 2)}\n`, "utf8");

  return {
    files: 1,
    templates: result.templates.length,
    resolved: result.resolved,
    unresolved: result.unresolved
  };
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (!args) {
    usage();
    process.exit(1);
  }

  const lootInput = path.resolve(args.lootInput);
  const outputPath = path.resolve(args.outputPath);
  const itemsDfnDir = path.resolve(args.itemsDfnDir);
  const allowUnresolved = args.allowUnresolved;

  const resolver = buildItemResolver(itemsDfnDir);
  const inputStat = fs.statSync(lootInput);

  if (inputStat.isFile()) {
    const stats = convertSingleLootFile(lootInput, outputPath, resolver, allowUnresolved);
    console.log(
      `Converted ${stats.files} file (${stats.templates} loot templates). Resolved=${stats.resolved}, Unresolved=${stats.unresolved}`
    );
    return;
  }

  if (!inputStat.isDirectory()) {
    throw new Error(`Unsupported input path: ${lootInput}`);
  }

  const lootFiles = collectFilesByExt(lootInput, ".dfn");
  let totalFiles = 0;
  let totalTemplates = 0;
  let totalResolved = 0;
  let totalUnresolved = 0;

  for (const lootFile of lootFiles) {
    const relative = path.relative(lootInput, lootFile);
    const outputFile = path.join(outputPath, relative).replace(/\.dfn$/i, ".json");
    const stats = convertSingleLootFile(lootFile, outputFile, resolver, allowUnresolved);
    totalFiles += stats.files;
    totalTemplates += stats.templates;
    totalResolved += stats.resolved;
    totalUnresolved += stats.unresolved;
  }

  console.log(
    `Converted ${totalFiles} loot dfn files (${totalTemplates} templates). Resolved=${totalResolved}, Unresolved=${totalUnresolved}`
  );
}

main();
