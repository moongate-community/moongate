#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";

function usage() {
  console.error(
    "Usage: node converters/modernuo-items-to-json.mjs <modernuo-items-dir> <output-items-dir>"
  );
}

function collectFilesByExt(rootDir, ext) {
  const files = [];
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
        files.push(fullPath);
      }
    }
  }

  files.sort((a, b) => a.localeCompare(b));
  return files;
}

function renderProgress(prefix, current, total, currentFile) {
  const safeTotal = total <= 0 ? 1 : total;
  const ratio = Math.min(1, Math.max(0, current / safeTotal));
  const width = 24;
  const filled = Math.round(ratio * width);
  const bar = `${"#".repeat(filled)}${"-".repeat(width - filled)}`;
  const percent = (ratio * 100).toFixed(1).padStart(5, " ");
  const fileText = currentFile ? ` ${currentFile}` : "";
  process.stdout.write(`\r${prefix} [${bar}] ${current}/${total} ${percent}%${fileText}`);
}

function finishProgress() {
  process.stdout.write("\n");
}

function toSnakeCase(name) {
  return name
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/([A-Z])([A-Z][a-z])/g, "$1_$2")
    .toLowerCase();
}

function toDisplayName(name) {
  return name
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z])([A-Z][a-z])/g, "$1 $2")
    .trim();
}

function normalizeHexInt(value) {
  if (value == null) {
    return null;
  }

  const text = String(value).trim();
  if (!text) {
    return null;
  }

  if (/^0x[0-9a-f]+$/i.test(text)) {
    return Number.parseInt(text, 16);
  }

  if (/^-?[0-9]+$/.test(text)) {
    return Number.parseInt(text, 10);
  }

  return null;
}

function formatItemId(numberValue) {
  const safe = Math.max(0, Number(numberValue) || 0);
  return `0x${safe.toString(16).toUpperCase().padStart(4, "0")}`;
}

function extractClassBlocks(content) {
  const results = [];
  const classRegex = /public\s+(?:partial\s+)?class\s+([A-Za-z0-9_]+)\s*:\s*([A-Za-z0-9_]+)/g;
  let match;

  while ((match = classRegex.exec(content)) !== null) {
    const className = match[1];
    const baseType = match[2];
    const classStart = match.index;
    const braceStart = content.indexOf("{", classRegex.lastIndex);
    if (braceStart < 0) {
      continue;
    }

    let depth = 1;
    let i = braceStart + 1;
    while (i < content.length && depth > 0) {
      const ch = content[i];
      if (ch === "{") {
        depth++;
      } else if (ch === "}") {
        depth--;
      }
      i++;
    }

    if (depth !== 0) {
      continue;
    }

    const classBody = content.slice(braceStart, i);
    const headerStart = content.lastIndexOf("\n", classStart - 1);
    const headerSlice = content.slice(Math.max(0, headerStart - 500), classStart);

    results.push({ className, baseType, classBody, headerSlice });
  }

  return results;
}

function extractFirstConstructibleItemId(className, classBody) {
  const escaped = className.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const ctorRegex = new RegExp(
    String.raw`\[Constructible\][\s\S]*?public\s+${escaped}\s*\([^)]*\)\s*:\s*base\s*\(([^)]*)\)`,
    "g"
  );
  let match;
  while ((match = ctorRegex.exec(classBody)) !== null) {
    const args = match[1]
      .split(",")
      .map((x) => x.trim())
      .filter(Boolean);
    for (const arg of args) {
      const numberValue = normalizeHexInt(arg);
      if (numberValue != null) {
        return numberValue;
      }
    }
  }
  return null;
}

function extractFlippableIds(headerSlice) {
  const flippableMatch = headerSlice.match(/\[Flippable\(([^)]*)\)\]/);
  if (!flippableMatch) {
    return [];
  }

  const values = flippableMatch[1]
    .split(",")
    .map((x) => normalizeHexInt(x))
    .filter((x) => x != null);

  return values;
}

function extractFirstNumberByRegex(classBody, regex) {
  const match = classBody.match(regex);
  if (!match) {
    return null;
  }

  const parsed = Number.parseFloat(match[1]);
  return Number.isFinite(parsed) ? parsed : null;
}

function extractTemplate(filePath, itemsRootDir, classInfo) {
  const { className, baseType, classBody, headerSlice } = classInfo;
  const itemId = extractFirstConstructibleItemId(className, classBody);
  if (itemId == null) {
    return null;
  }

  const rel = path.relative(itemsRootDir, filePath);
  const segments = rel.split(path.sep);
  const category = segments.length > 1 ? segments[0] : "Misc";

  const id = toSnakeCase(className);
  const template = {
    type: "item",
    id,
    name: toDisplayName(className),
    category,
    description: `Imported from ModernUO (${className}).`,
    itemId: formatItemId(itemId),
    hue: "0",
    goldValue: "0",
    weight: 0,
    scriptId: `items.${id}`,
    isMovable: true,
    tags: ["modernuo", toSnakeCase(category)]
  };

  const flippables = extractFlippableIds(headerSlice);
  if (flippables.length > 0) {
    template.tags.push("flippable");
  }

  const weight = extractFirstNumberByRegex(classBody, /DefaultWeight\s*=>\s*([0-9]+(?:\.[0-9]+)?)/);
  if (weight != null) {
    template.weight = weight;
  }

  const minDamage = extractFirstNumberByRegex(classBody, /AosMinDamage\s*=>\s*(-?[0-9]+)/);
  const maxDamage = extractFirstNumberByRegex(classBody, /AosMaxDamage\s*=>\s*(-?[0-9]+)/);
  const oldMinDamage = extractFirstNumberByRegex(classBody, /OldMinDamage\s*=>\s*(-?[0-9]+)/);
  const oldMaxDamage = extractFirstNumberByRegex(classBody, /OldMaxDamage\s*=>\s*(-?[0-9]+)/);
  const speed = extractFirstNumberByRegex(classBody, /AosSpeed\s*=>\s*(-?[0-9]+)/);
  const oldSpeed = extractFirstNumberByRegex(classBody, /OldSpeed\s*=>\s*(-?[0-9]+)/);
  const strength = extractFirstNumberByRegex(classBody, /AosStrengthReq\s*=>\s*(-?[0-9]+)/);
  const oldStrength = extractFirstNumberByRegex(classBody, /OldStrengthReq\s*=>\s*(-?[0-9]+)/);
  const initMaxHits = extractFirstNumberByRegex(classBody, /InitMaxHits\s*=>\s*(-?[0-9]+)/);
  const maxItems = extractFirstNumberByRegex(classBody, /MaxItems\s*=>\s*(-?[0-9]+)/);
  const maxWeight = extractFirstNumberByRegex(classBody, /MaxWeight\s*=>\s*(-?[0-9]+)/);
  const hue = extractFirstNumberByRegex(classBody, /\bHue\s*=\s*(0x[0-9a-fA-F]+|[0-9]+)/);

  if (minDamage != null || oldMinDamage != null) {
    template.lowDamage = Number(minDamage ?? oldMinDamage);
  }

  if (maxDamage != null || oldMaxDamage != null) {
    template.highDamage = Number(maxDamage ?? oldMaxDamage);
  }

  if (speed != null || oldSpeed != null) {
    template.speed = Number(speed ?? oldSpeed);
  }

  if (strength != null || oldStrength != null) {
    template.strength = Number(strength ?? oldStrength);
  }

  if (initMaxHits != null) {
    template.hitPoints = Number(initMaxHits);
  }

  if (maxItems != null) {
    template.maxItems = Number(maxItems);
  }

  if (maxWeight != null) {
    template.weightMax = Number(maxWeight);
  }

  if (hue != null) {
    template.hue = String(Number(hue));
  }

  return template;
}

function writeTemplatesByCategory(templates, outputDir) {
  fs.mkdirSync(outputDir, { recursive: true });
  const byCategory = new Map();

  for (const template of templates) {
    const key = toSnakeCase(template.category || "misc");
    const bucket = byCategory.get(key);
    if (bucket) {
      bucket.push(template);
    } else {
      byCategory.set(key, [template]);
    }
  }

  for (const [categoryKey, bucket] of byCategory) {
    bucket.sort((a, b) => a.id.localeCompare(b.id));
    const outFile = path.join(outputDir, `${categoryKey}.json`);
    fs.writeFileSync(outFile, `${JSON.stringify(bucket, null, 2)}\n`, "utf8");
  }
}

function main() {
  const [, , itemsRootDir, outputDir] = process.argv;
  if (!itemsRootDir || !outputDir) {
    usage();
    process.exit(1);
  }

  const resolvedItemsRoot = path.resolve(itemsRootDir);
  const resolvedOutput = path.resolve(outputDir);

  if (!fs.existsSync(resolvedItemsRoot)) {
    console.error(`Items root not found: ${resolvedItemsRoot}`);
    process.exit(1);
  }

  const files = collectFilesByExt(resolvedItemsRoot, ".cs");
  const templates = [];
  let classCount = 0;

  for (let i = 0; i < files.length; i++) {
    const file = files[i];
    const rel = path.relative(resolvedItemsRoot, file);
    renderProgress("Scanning ModernUO items", i + 1, files.length, rel);

    const content = fs.readFileSync(file, "utf8");
    const classes = extractClassBlocks(content);
    classCount += classes.length;

    for (const classInfo of classes) {
      const template = extractTemplate(file, resolvedItemsRoot, classInfo);
      if (template) {
        templates.push(template);
      }
    }
  }

  finishProgress();

  const uniqueById = new Map();
  for (const template of templates) {
    uniqueById.set(template.id, template);
  }

  const deduped = Array.from(uniqueById.values());
  writeTemplatesByCategory(deduped, resolvedOutput);

  console.log(`Processed files: ${files.length}`);
  console.log(`Scanned classes: ${classCount}`);
  console.log(`Generated templates: ${deduped.length}`);
  console.log(`Output directory: ${resolvedOutput}`);
}

main();
