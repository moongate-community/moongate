import { execFileSync } from 'node:child_process';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const websiteRoot = fileURLToPath(new URL('../', import.meta.url));
const repositoryRoot = path.resolve(websiteRoot, '..');
const validTypes = new Set(['byte', 'sbyte', 'bool', 'short', 'ushort', 'int', 'uint', 'long', 'ulong', 'ascii', 'utf8', 'enum', 'bitfield', 'byte[]', 'loop']);
const directions = new Set(['incoming', 'outgoing', 'both']);

function opcodeParts(id) {
  if (!/^0x[\dA-F]{2}(?:\/0x[\dA-F]{2,4})?$/.test(id)) throw new Error(`Invalid packet id: ${id}`);
  return id.split('/').map(part => Number.parseInt(part.slice(2), 16));
}

export function mergePackets(dump, overrides) {
  const seen = new Set();
  const packets = dump.map(packet => {
    opcodeParts(packet.id);
    if (!directions.has(packet.direction)) throw new Error(`Invalid direction: ${packet.direction}`);
    if (!['fixed', 'variable'].includes(packet.sizing) || !Number.isInteger(packet.minimumLength) || packet.minimumLength < 1) {
      throw new Error(`Invalid sizing for ${packet.name}`);
    }
    const key = `${packet.id}:${packet.direction}`;
    if (seen.has(key)) throw new Error(`Conflicting packet identity: ${key}`);
    seen.add(key);
    const override = overrides[key];
    if (!override) throw new Error(`Missing override for ${key}`);
    if (typeof override.category !== 'string' || !override.category.trim() ||
        !Array.isArray(override.tags) || override.tags.some(tag => typeof tag !== 'string' || !tag.trim()) ||
        !Array.isArray(override.fields) || !override.fields.length ||
        typeof override.notes !== 'string' || typeof override.handler !== 'string') {
      throw new Error(`Invalid override for ${key}`);
    }
    const occupied = [];
    for (const field of override.fields) {
      if (!validTypes.has(field.type)) throw new Error(`Invalid field type ${field.type} for ${key}`);
      if (['offset', 'name', 'size', 'description'].some(name => typeof field[name] !== 'string')) {
        throw new Error(`Invalid field for ${key}`);
      }
      if (!/^\d+$/.test(field.offset) || !/^(?:\d+|variable)$/.test(field.size)) {
        throw new Error(`Invalid field position for ${key}`);
      }
      if (field.size !== 'variable') {
        const start = Number(field.offset), end = start + Number(field.size);
        if (end <= start) throw new Error(`Invalid field size for ${key}`);
        if (packet.sizing === 'fixed' && end > packet.fixedLength) throw new Error(`Field outside fixed frame for ${key}`);
        if (occupied.some(([otherStart, otherEnd]) => start < otherEnd && end > otherStart)) {
          throw new Error(`Overlapping fields for ${key}`);
        }
        occupied.push([start, end]);
      }
    }
    if (packet.sizing === 'fixed' && (!Number.isInteger(packet.fixedLength) || packet.fixedLength !== packet.minimumLength)) {
      throw new Error(`Invalid fixed length for ${key}`);
    }
    return {
      id: packet.id, name: packet.name, direction: packet.direction,
      size: packet.sizing === 'fixed'
        ? { kind: 'fixed', bytes: packet.fixedLength }
        : { kind: 'dynamic', minimum: packet.minimumLength },
      description: packet.description, source: packet.source,
      category: override.category, tags: override.tags,
      fields: override.fields, notes: override.notes, handler: override.handler,
    };
  });
  for (const key of Object.keys(overrides)) if (!seen.has(key)) throw new Error(`Unknown override: ${key}`);
  return packets.sort((left, right) => {
    const a = opcodeParts(left.id), b = opcodeParts(right.id);
    return a[0] - b[0] || (a[1] ?? -1) - (b[1] ?? -1) ||
      ['incoming', 'outgoing', 'both'].indexOf(left.direction) - ['incoming', 'outgoing', 'both'].indexOf(right.direction);
  });
}

function assertDocumentedClasses(packets, markdown) {
  const names = new Set(packets.map(packet => packet.name));
  const tableClasses = [...markdown.matchAll(/^\| `0x[\dA-F]{2}`[^\n]+$/gm)]
    .flatMap(([row]) => [...row.split('|')[2].matchAll(/`([A-Za-z]+Packet)`/g)].map(match => match[1]));
  for (const name of tableClasses) if (!names.has(name)) throw new Error(`Documented packet missing from dump: ${name}`);
}

export async function buildPackets() {
  const project = path.join(websiteRoot, 'packets/dump/PacketDump.csproj');
  const stage = await mkdtemp(path.join(tmpdir(), 'moongate-packets-'));
  let dump;
  try {
    const dumpFile = path.join(stage, 'packets.json');
    execFileSync('dotnet', ['run', '--project', project, '--configuration', 'Release', '--no-launch-profile', '--verbosity', 'quiet', '--', dumpFile], {
      cwd: repositoryRoot, stdio: ['ignore', 'pipe', 'inherit'], maxBuffer: 8 * 1024 * 1024,
    });
    dump = JSON.parse(await readFile(dumpFile, 'utf8'));
  } finally {
    await rm(stage, { recursive: true, force: true });
  }
  const overrides = JSON.parse(await readFile(path.join(websiteRoot, 'packets/overrides.json'), 'utf8'));
  const packets = mergePackets(dump, overrides);
  assertDocumentedClasses(packets, await readFile(path.join(repositoryRoot, 'docs/packets.md'), 'utf8'));
  const output = path.join(websiteRoot, 'src/generated/packets.json');
  await mkdir(path.dirname(output), { recursive: true });
  await writeFile(output, `${JSON.stringify(packets, null, 2)}\n`);
  console.log(`Generated ${packets.length} packet definitions.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) await buildPackets();
