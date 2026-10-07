import { execFileSync } from 'node:child_process';
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
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
// The overview and the enums page: a module with one of these names would overwrite it.
const reservedPages = new Set(['index', 'enums']);
const byName = (left, right) => (left.name < right.name ? -1 : left.name > right.name ? 1 : 0);
const blank = text => typeof text !== 'string' || !text.trim();

// Splits website/lua/examples/<module>.md into one Markdown body per function: each "## <function>" heading
// starts a section. The bodies are written by hand in the repository, so they reach the page as Markdown.
export function parseExamples(module, markdown) {
  const examples = new Map();
  let name = null, body = [], fenced = false;
  const close = () => {
    if (name === null) return;
    const text = body.join('\n').trim();
    if (!text) throw new Error(`Empty example: ${module}.${name}`);
    examples.set(name, text);
  };
  for (const line of markdown.split('\n')) {
    if (line.startsWith('```')) fenced = !fenced;
    const heading = fenced ? null : line.match(/^## (.+?)\s*$/);
    if (heading) {
      close();
      name = heading[1];
      if (examples.has(name)) throw new Error(`Duplicate example: ${module}.${name}`);
      body = [];
    } else if (name === null) {
      if (line.trim()) throw new Error(`${module}: text before the first function heading`);
    } else {
      // Another heading would look like a second function on the page, with an anchor of its own.
      if (!fenced && /^#{1,6} /.test(line)) throw new Error(`${module}.${name}: a heading inside an example`);
      body.push(line);
    }
  }
  // An open code block would take the parameter table and every function after it.
  if (fenced) throw new Error(`${module}: unclosed code block`);
  close();
  if (!examples.size) throw new Error(`${module}: no examples`);
  return examples;
}

// Reads every <module>.md of the examples directory; a missing directory means no examples.
export async function readExamples(directory) {
  let files;
  try {
    files = await readdir(directory);
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
    return {};
  }
  const examples = {};
  for (const file of files.filter(name => name.endsWith('.md')).sort()) {
    const module = path.basename(file, '.md');
    examples[module] = parseExamples(module, await readFile(path.join(directory, file), 'utf8'));
  }
  return examples;
}

function validateExamples(dump, examples) {
  for (const [name, bodies] of Object.entries(examples)) {
    const module = dump.modules.find(item => item.name === name);
    if (!module) throw new Error(`Examples for an unknown module: ${name}`);
    for (const fn of bodies.keys()) {
      if (!module.functions.some(item => item.name === fn)) throw new Error(`Example for an unknown function: ${name}.${fn}`);
    }
  }
}

function validate(dump) {
  const modules = new Set();
  for (const module of dump.modules) {
    if (!luaName.test(module.name) || reservedPages.has(module.name)) throw new Error(`Invalid module name: ${module.name}`);
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

// Everything that comes from a C# attribute is written as HTML, never as Markdown: help text holds Lua such as
// g:text{...}, ~1_NAME~ and { kind = 'object' }, which Markdown would read as a directive, strike through or
// print with curled quotes. An HTML block ends at a blank line, so none of these writes one.
const html = value => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
const code = value => `<code>${html(value)}</code>`;
const paragraphs = value => value.trim().split(/\n\s*\n/)
  .map(paragraph => `<p>${paragraph.split('\n').map(line => html(line.trim())).join('<br>\n')}</p>`);
const inline = value => html(value.trim()).replace(/\s*\n\s*/g, ' ');

function table(headers, rows) {
  return [
    '<table>',
    `<thead><tr>${headers.map(header => `<th>${header}</th>`).join('')}</tr></thead>`,
    '<tbody>',
    ...rows.map(cells => `<tr>${cells.map(cell => `<td>${cell}</td>`).join('')}</tr>`),
    '</tbody>',
    '</table>',
  ];
}

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

// "DirectionType|string" becomes one code span per alternative, with the enum linked to its heading.
function typeHtml(type, enums) {
  return type.split('|').map(part => {
    const enumName = part.replace(/\?$/, '');
    return enums.has(enumName) ? `<a href="${docsBasePath}lua/enums/#${enumName.toLowerCase()}">${code(part)}</a>` : code(part);
  }).join(' | ');
}

function functionSection(module, fn, enums, example) {
  const names = fn.parameters.map(parameter => parameter.name + (parameter.optional ? '?' : ''));
  const signature = `${module.name}.${fn.name}(${names.join(', ')})${fn.returns ? ` -> ${fn.returns}` : ''}`;
  const lines = [`### ${fn.name}`, '', '```lua', signature, '```', ''];
  for (const paragraph of paragraphs(fn.help)) lines.push(paragraph, '');
  if (example) lines.push(example, '');
  if (fn.parameters.length) {
    lines.push(...table(['Parameter', 'Type', 'Default'], fn.parameters.map(parameter => [
      code(parameter.name) + (parameter.optional ? ' (optional)' : ''),
      typeHtml(parameter.type, enums),
      parameter.optional ? code(parameter.default ?? 'nil') : '',
    ])), '');
  }
  if (fn.returns) lines.push(`<p>Returns ${typeHtml(fn.returns, enums)}.</p>`, '');
  return lines;
}

function modulePage(module, enums, sourceRef, examples) {
  const lines = [frontMatter({ title: module.name, description: module.description, order: 1 })];
  for (const paragraph of paragraphs(module.description)) lines.push(paragraph, '');
  lines.push(`Source: [\`${module.source}\`](${repositoryUrl}/blob/${sourceRef}/${module.source})`, '');
  if (module.functions.length) {
    lines.push('## Functions', '');
    for (const fn of [...module.functions].sort(byName)) lines.push(...functionSection(module, fn, enums, examples?.get(fn.name)));
  }
  if (module.constants.length) {
    // A null value is a constant the server computes when it starts, such as its version: the dump cannot know it.
    lines.push('## Constants', '', ...table(['Name', 'Type', 'Value', 'Description'], [...module.constants].sort(byName).map(constant => [
      code(constant.name),
      typeHtml(constant.type, enums),
      constant.value === null ? 'set when the server starts' : code(constant.value),
      inline(constant.help ?? ''),
    ])), '');
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
    '## Modules', '',
    ...table(['Module', 'Description'], modules.map(module => [
      `<a href="${docsBasePath}lua/${module.name}/">${code(module.name)}</a>`, inline(module.description),
    ])), '',
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
export function renderLua(dump, { sourceRef = 'develop', examples = {} } = {}) {
  validate(dump);
  validateExamples(dump, examples);
  const modules = [...dump.modules].sort(byName);
  const enums = [...dump.enums].sort(byName);
  const enumNames = new Set(enums.map(item => item.name));
  const pages = new Map([['index.md', overviewPage(modules)], ['enums.md', enumsPage(enums)]]);
  for (const module of modules) pages.set(`${module.name}.md`, modulePage(module, enumNames, sourceRef, examples[module.name]));
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
  const examples = await readExamples(path.join(websiteRoot, 'lua/examples'));
  const pages = renderLua(dump, { sourceRef: process.env.MOONGATE_DOCS_REF || 'develop', examples });
  await writePages(path.join(websiteRoot, 'src/content/docs/lua'), pages);
  const functions = dump.modules.reduce((count, module) => count + module.functions.length, 0);
  const withExamples = Object.values(examples).reduce((count, bodies) => count + bodies.size, 0);
  console.log(`Generated the Lua reference: ${dump.modules.length} modules, ${functions} functions (${withExamples} with an example), ${dump.enums.length} enums.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) await buildLua();
