import { execFileSync } from 'node:child_process';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { docsBasePath } from '../site-config.mjs';

const websiteRoot = fileURLToPath(new URL('../', import.meta.url));
const repositoryRoot = path.resolve(websiteRoot, '..');
const repositoryUrl = 'https://github.com/moongate-community/moongate';
const luaName = /^[a-z_][a-z0-9_]*$/;
const constantName = /^[A-Za-z_][A-Za-z0-9_]*$/;
// The headings a module page already has: a function with one of these names would take its anchor.
const sectionAnchors = new Set(['functions', 'constants']);
const byName = (left, right) => (left.name < right.name ? -1 : left.name > right.name ? 1 : 0);
const blank = text => typeof text !== 'string' || !text.trim();

function validate(dump) {
  const modules = new Set();
  for (const module of dump.modules) {
    if (!luaName.test(module.name)) throw new Error(`Invalid module name: ${module.name}`);
    if (modules.has(module.name)) throw new Error(`Duplicate module: ${module.name}`);
    modules.add(module.name);
    if (blank(module.description)) throw new Error(`Module ${module.name} has no description`);
    const functions = new Set();
    for (const fn of module.functions) {
      const name = `${module.name}.${fn.name}`;
      if (!luaName.test(fn.name) || sectionAnchors.has(fn.name)) throw new Error(`Invalid function name: ${name}`);
      if (functions.has(fn.name)) throw new Error(`Duplicate function: ${name}`);
      functions.add(fn.name);
      if (blank(fn.help)) throw new Error(`Function ${name} has no help text`);
      for (const parameter of fn.parameters) {
        if (blank(parameter.type)) throw new Error(`${name}: parameter ${parameter.name} has no Lua type`);
      }
      if (fn.returns !== null && blank(fn.returns)) throw new Error(`${name}: empty return type`);
    }
    for (const constant of module.constants) {
      if (!constantName.test(constant.name)) throw new Error(`Invalid constant name: ${module.name}.${constant.name}`);
    }
  }
}

// Help text is prose from C# attributes: keep it from opening an HTML tag.
const text = value => value.replaceAll('<', '&lt;');
const cell = value => text(value).replaceAll('|', '\\|').replace(/\s*\n\s*/g, ' ');

function frontMatter({ title, description, order, label }) {
  return [
    '---',
    `title: ${JSON.stringify(title)}`,
    `description: ${JSON.stringify(description)}`,
    'editUrl: false',
    'sidebar:',
    ...(label ? [`  label: ${JSON.stringify(label)}`] : []),
    `  order: ${order}`,
    '---', '',
  ].join('\n');
}

// "DirectionType|string" becomes one code span per alternative, the enum linked, the pipe escaped for a table.
function typeCell(type, enums) {
  return type.split('|').map(part => {
    const code = `\`${part}\``;
    const enumName = part.replace(/\?$/, '');
    return enums.has(enumName) ? `[${code}](${docsBasePath}lua/enums/#${enumName.toLowerCase()})` : code;
  }).join('\\|');
}

function functionSection(module, fn, enums) {
  const names = fn.parameters.map(parameter => parameter.name + (parameter.optional ? '?' : ''));
  const signature = `${module.name}.${fn.name}(${names.join(', ')})${fn.returns ? ` -> ${fn.returns}` : ''}`;
  const lines = [`### ${fn.name}`, '', '```lua', signature, '```', '', text(fn.help.trim()), ''];
  if (fn.parameters.length) {
    lines.push('| Parameter | Type | Default |', '| --- | --- | --- |');
    for (const parameter of fn.parameters) {
      const name = `\`${parameter.name}\`${parameter.optional ? ' (optional)' : ''}`;
      const fallback = parameter.optional ? ` \`${parameter.default ?? 'nil'}\` ` : ' ';
      lines.push(`| ${name} | ${typeCell(parameter.type, enums)} |${fallback}|`);
    }
    lines.push('');
  }
  return lines;
}

function modulePage(module, enums, sourceRef) {
  const lines = [
    frontMatter({ title: module.name, description: module.description, order: 1 }),
    text(module.description.trim()), '',
    `Source: [\`${module.source}\`](${repositoryUrl}/blob/${sourceRef}/${module.source})`, '',
  ];
  if (module.functions.length) {
    lines.push('## Functions', '');
    for (const fn of [...module.functions].sort(byName)) lines.push(...functionSection(module, fn, enums));
  }
  if (module.constants.length) {
    lines.push('## Constants', '', '| Name | Type | Value | Description |', '| --- | --- | --- | --- |');
    for (const constant of [...module.constants].sort(byName)) {
      lines.push(`| \`${constant.name}\` | ${typeCell(constant.type, enums)} | \`${constant.value}\` | ${cell(constant.help ?? '')} |`);
    }
    lines.push('');
  }
  return lines.join('\n');
}

function overviewPage(modules) {
  const lines = [
    frontMatter({
      title: 'Lua API reference', label: 'Overview', order: 0,
      description: 'Every module, function, constant and enum the server publishes to Lua scripts.',
    }),
    'Every module, function, constant and enum the server publishes to Lua scripts. These pages are generated',
    `from the server's code on every build. To learn how scripts are written and loaded, read`,
    `[Writing Lua scripts](${docsBasePath}server/scripting/).`, '',
    '## Modules', '', '| Module | Description |', '| --- | --- |',
    ...modules.map(module => `| [\`${module.name}\`](${docsBasePath}lua/${module.name}/) | ${cell(module.description.trim())} |`), '',
    '## Globals', '',
    '### wait', '', '```lua', 'wait(seconds) -> number', '```', '',
    'Suspends the running coroutine and resumes it on the game loop after the given seconds; it returns the',
    'seconds actually waited.', '',
    '### print', '', '```lua', 'print(...)', '```', '',
    'Writes its values, separated by tabs, to the server log at Information level.', '',
    '## Enums', '',
    `Every published enum is a read-only global table, keyed by member name: see [Enums](${docsBasePath}lua/enums/).`, '',
  ];
  return lines.join('\n');
}

function enumsPage(enums) {
  const lines = [
    frontMatter({
      title: 'Enums', order: 2,
      description: 'The enums published to Lua scripts as read-only global tables.',
    }),
    'Each enum is a read-only global table: `DirectionType.North` is a number. A function that takes an enum',
    'also takes the member name as a string.', '',
  ];
  for (const item of enums) {
    lines.push(`## ${item.name}`, '', '| Member | Value |', '| --- | --- |');
    for (const member of item.members) lines.push(`| \`${member.name}\` | ${member.value} |`);
    lines.push('');
  }
  return lines.join('\n');
}

// Returns the pages as file name -> Markdown; nothing is written, so a rejected dump leaves the site as it was.
export function renderLua(dump, { sourceRef = 'develop' } = {}) {
  validate(dump);
  const modules = [...dump.modules].sort(byName);
  const enums = [...dump.enums].sort(byName);
  const enumNames = new Set(enums.map(item => item.name));
  const pages = new Map([['index.md', overviewPage(modules)], ['enums.md', enumsPage(enums)]]);
  for (const module of modules) pages.set(`${module.name}.md`, modulePage(module, enumNames, sourceRef));
  return pages;
}

// The directory holds generated pages only: replace it whole, so a module the server dropped loses its page.
export async function writePages(directory, pages) {
  await rm(directory, { recursive: true, force: true });
  await mkdir(directory, { recursive: true });
  for (const [name, markdown] of pages) await writeFile(path.join(directory, name), markdown);
}

export async function buildLua() {
  const project = path.join(websiteRoot, 'lua/dump/LuaDump.csproj');
  const stage = await mkdtemp(path.join(tmpdir(), 'moongate-lua-'));
  let dump;
  try {
    const dumpFile = path.join(stage, 'lua.json');
    execFileSync('dotnet', ['run', '--project', project, '--configuration', 'Release', '--no-launch-profile', '--verbosity', 'quiet', '--', dumpFile], {
      cwd: repositoryRoot, stdio: ['ignore', 'pipe', 'inherit'], maxBuffer: 8 * 1024 * 1024,
    });
    dump = JSON.parse(await readFile(dumpFile, 'utf8'));
  } finally {
    await rm(stage, { recursive: true, force: true });
  }
  const pages = renderLua(dump, { sourceRef: process.env.MOONGATE_DOCS_REF || 'develop' });
  await writePages(path.join(websiteRoot, 'src/content/docs/lua'), pages);
  const functions = dump.modules.reduce((count, module) => count + module.functions.length, 0);
  console.log(`Generated the Lua reference: ${dump.modules.length} modules, ${functions} functions, ${dump.enums.length} enums.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) await buildLua();
