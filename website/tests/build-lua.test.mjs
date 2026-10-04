import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { parseExamples, readExamples, renderLua, writePages } from '../scripts/build-lua.mjs';

const parameter = (name, type = 'integer', optional = false, fallback = null) => ({ name, type, optional, default: fallback });
const fn = (name, parameters = [], returns = null, help = `Does ${name}.`) => ({ name, help, parameters, returns });
const module = (name, functions = [], constants = [], description = `The ${name} module.`) => ({
  name, description, source: `src/Moongate.Server.Ultima/Modules/${name}.cs`, functions, constants,
});
const sample = () => ({
  modules: [
    module('npc', [
      fn('walk_to', [parameter('serial'), parameter('z', 'integer', true), parameter('running', 'boolean', true, 'false')], 'string?',
        'Walks <b>fast</b> & far, answers a|b or `nil`.\nSecond line.'),
      fn('say', [parameter('...', 'any')]),
      fn('step', [parameter('direction', 'DirectionType|string'), parameter('again', 'DirectionType?', true)], 'DirectionType'),
    ]),
    module('log', [], [
      { name: 'LEVEL_DEBUG', type: 'integer', value: '1', help: 'The a|b level.' },
      { name: 'codename', type: 'string', value: null, help: 'Release codename.' },
      { name: 'separator', type: 'string', value: '"a|b`c"', help: null },
    ]),
    module('gump', [
      fn('create', [], 'table', "A gump built in code: g:text{...}, g:button{...}, then { kind = 'object' } or \"\"."),
      fn('cliloc', [], null, 'Fills ~1_NAME~ and ~2_COUNT~.\n# not a heading\n1. not a list'),
    ]),
  ],
  enums: [{ name: 'DirectionType', members: [{ name: 'North', value: 0 }, { name: 'Right', value: 1 }] }],
});

test('render writes an overview, one page per module and the enums', () => {
  const pages = renderLua(sample());
  assert.deepEqual([...pages.keys()].sort(), ['enums.md', 'gump.md', 'index.md', 'log.md', 'npc.md']);
  for (const page of pages.values()) assert.match(page, /^---\ntitle: "/);
});

test('a module page gives each function a heading, its signature, its help and its parameters', () => {
  const page = renderLua(sample(), { sourceRef: 'v1.2.3' }).get('npc.md');
  assert.match(page, /^title: "npc"$/m);
  assert.match(page, /^<p>The npc module\.<\/p>$/m);
  assert.ok(page.includes('[`src/Moongate.Server.Ultima/Modules/npc.cs`](https://github.com/moongate-community/moongate/blob/v1.2.3/src/Moongate.Server.Ultima/Modules/npc.cs)'));
  assert.ok(page.includes('### walk_to\n\n```lua\nnpc.walk_to(serial, z?, running?) -> string?\n```'));
  assert.ok(page.includes('<tr><td><code>serial</code></td><td><code>integer</code></td><td></td></tr>'));
  assert.ok(page.includes('<tr><td><code>z</code> (optional)</td><td><code>integer</code></td><td><code>nil</code></td></tr>'));
  assert.ok(page.includes('<tr><td><code>running</code> (optional)</td><td><code>boolean</code></td><td><code>false</code></td></tr>'));
  // Functions are listed by name, whatever the dump's order.
  assert.ok(page.indexOf('### say') < page.indexOf('### step') && page.indexOf('### step') < page.indexOf('### walk_to'));
});

test('help text keeps its lines and is never read as Markdown or HTML', () => {
  const pages = renderLua(sample());
  assert.ok(pages.get('npc.md').includes('<p>Walks &lt;b&gt;fast&lt;/b&gt; &amp; far, answers a|b or `nil`.<br>\nSecond line.</p>'));
  // Starlight reads :text{...} as a directive, ~x~ as strikethrough and curls quotes: none of that may reach help text.
  assert.ok(pages.get('gump.md').includes(`<p>A gump built in code: g:text{...}, g:button{...}, then { kind = 'object' } or "".</p>`));
  assert.ok(pages.get('gump.md').includes('<p>Fills ~1_NAME~ and ~2_COUNT~.<br>\n# not a heading<br>\n1. not a list</p>'));
});

test('no blank line falls inside an HTML block, which would hand the rest back to Markdown', () => {
  for (const [name, page] of renderLua(sample())) {
    for (const block of page.split('\n\n')) {
      const opened = (block.match(/<(p|table)>/g) ?? []).length;
      const closed = (block.match(/<\/(p|table)>/g) ?? []).length;
      assert.equal(opened, closed, `${name}: ${block}`);
    }
  }
});

test('varargs show as ... and a function that returns nothing has no arrow', () => {
  const page = renderLua(sample()).get('npc.md');
  assert.ok(page.includes('```lua\nnpc.say(...)\n```'));
  assert.ok(page.includes('<tr><td><code>...</code></td><td><code>any</code></td><td></td></tr>'));
  assert.ok(!page.slice(page.indexOf('### say'), page.indexOf('### step')).includes('Returns'));
});

test('an enum type links to the enums page, in a parameter and in a return', () => {
  const page = renderLua(sample()).get('npc.md');
  const link = '<a href="/lua/enums/#directiontype"><code>DirectionType</code></a>';
  assert.ok(page.includes(`<tr><td><code>direction</code></td><td>${link} | <code>string</code></td><td></td></tr>`));
  assert.ok(page.includes('<td><a href="/lua/enums/#directiontype"><code>DirectionType?</code></a></td>'));
  assert.ok(page.includes('npc.step(direction, again?) -> DirectionType'));
  assert.ok(page.includes(`<p>Returns ${link}.</p>`));
  assert.ok(page.includes('<p>Returns <code>string?</code>.</p>'));
});

test('a section without rows is left out', () => {
  const pages = renderLua(sample());
  assert.ok(pages.get('log.md').includes('## Constants'));
  assert.ok(!pages.get('log.md').includes('## Functions'));
  assert.ok(pages.get('log.md').includes('<tr><td><code>LEVEL_DEBUG</code></td><td><code>integer</code></td><td><code>1</code></td><td>The a|b level.</td></tr>'));
  assert.ok(!pages.get('npc.md').includes('## Constants'));
});

test('a constant read from a property shows no value: the server sets it when it starts', () => {
  const page = renderLua(sample()).get('log.md');
  assert.ok(page.includes('<tr><td><code>codename</code></td><td><code>string</code></td><td>set when the server starts</td><td>Release codename.</td></tr>'));
  assert.ok(page.includes('<tr><td><code>separator</code></td><td><code>string</code></td><td><code>"a|b`c"</code></td><td></td></tr>'));
});

test('the overview lists the modules by name, the globals and the enums', () => {
  const page = renderLua(sample()).get('index.md');
  assert.ok(page.indexOf('<a href="/lua/log/">') < page.indexOf('<a href="/lua/npc/">'));
  assert.ok(page.includes('<tr><td><a href="/lua/npc/"><code>npc</code></a></td><td>The npc module.</td></tr>'));
  assert.match(page, /^## Globals$/m);
  assert.ok(page.includes('wait(seconds) -> number'));
  assert.ok(page.includes('print(...)'));
  assert.ok(page.includes('(/lua/enums/)'));
});

test('the enums page gives each enum a heading and its members', () => {
  const page = renderLua(sample()).get('enums.md');
  assert.match(page, /^## DirectionType$/m);
  assert.ok(page.includes('| `North` | 0 |'));
  assert.ok(page.includes('| `Right` | 1 |'));
});

test('render rejects a module without a description and a function without help', () => {
  const noDescription = sample();
  noDescription.modules[0].description = null;
  assert.throws(() => renderLua(noDescription), /Module npc has no description/);
  const noHelp = sample();
  noHelp.modules[0].functions[1].help = ' ';
  assert.throws(() => renderLua(noHelp), /Function npc\.say has no help text/);
});

test('render rejects names that are not anchors or that repeat', () => {
  const badModule = sample();
  badModule.modules.find(item => item.name === 'log').name = 'Log';
  assert.throws(() => renderLua(badModule), /Invalid module name: Log/);
  const badFunction = sample();
  badFunction.modules[0].functions[0].name = 'walk-to';
  assert.throws(() => renderLua(badFunction), /Invalid function name: npc\.walk-to/);
  const reserved = sample();
  reserved.modules[0].functions[0].name = 'functions';
  assert.throws(() => renderLua(reserved), /Invalid function name: npc\.functions/);
  const repeatedModule = sample();
  repeatedModule.modules.find(item => item.name === 'log').name = 'npc';
  assert.throws(() => renderLua(repeatedModule), /Duplicate module: npc/);
  const repeatedFunction = sample();
  repeatedFunction.modules[0].functions[1].name = 'step';
  assert.throws(() => renderLua(repeatedFunction), /Duplicate function: npc\.step/);
  const badConstant = sample();
  badConstant.modules.find(item => item.name === 'log').constants[0].name = 'LEVEL DEBUG';
  assert.throws(() => renderLua(badConstant), /Invalid constant name: log\.LEVEL DEBUG/);
});

test('render rejects a module named after the overview or the enums page, which it would overwrite', () => {
  for (const name of ['index', 'enums']) {
    const taken = sample();
    taken.modules[0].name = name;
    assert.throws(() => renderLua(taken), new RegExp(`Invalid module name: ${name}`));
  }
});

test('render rejects a parameter or a return without a Lua type', () => {
  const noType = sample();
  noType.modules[0].functions[0].parameters[1].type = '';
  assert.throws(() => renderLua(noType), /npc\.walk_to: parameter z has no Lua type/);
  const noReturn = sample();
  noReturn.modules[0].functions[0].returns = '';
  assert.throws(() => renderLua(noReturn), /npc\.walk_to: empty return type/);
});

test('writing the pages replaces the directory, so a removed module leaves no page behind', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'moongate-lua-test-'));
  try {
    const directory = path.join(root, 'lua');
    await mkdir(directory);
    await writeFile(path.join(directory, 'old.md'), 'stale');
    await writePages(directory, renderLua(sample()));
    assert.deepEqual((await readdir(directory)).sort(), ['enums.md', 'gump.md', 'index.md', 'log.md', 'npc.md']);
    assert.match(await readFile(path.join(directory, 'npc.md'), 'utf8'), /### walk_to/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

const examplesText = [
  '## walk_to', '', 'Call it on every think:', '', '```lua', 'local state = npc.walk_to(serial, 1434, 1699)', '```', '',
  '## say', '', 'A note with `code` and a [link](https://moongate.sh/lua/).', '',
].join('\n');

test('an examples file is split into one body per function', () => {
  const examples = parseExamples('npc', examplesText);
  assert.deepEqual([...examples.keys()], ['walk_to', 'say']);
  assert.equal(examples.get('walk_to'), 'Call it on every think:\n\n```lua\nlocal state = npc.walk_to(serial, 1434, 1699)\n```');
  assert.equal(examples.get('say'), 'A note with `code` and a [link](https://moongate.sh/lua/).');
});

test('a heading inside a code block of an example is not a section', () => {
  const examples = parseExamples('npc', '## say\n\n```lua\n## not a heading\n```\n');
  assert.deepEqual([...examples.keys()], ['say']);
});

test('an examples file is rejected for text before the first section, a repeated section or an empty one', () => {
  assert.throws(() => parseExamples('npc', 'stray\n\n## say\n\ntext\n'), /npc: text before the first function/);
  assert.throws(() => parseExamples('npc', '## say\n\na\n\n## say\n\nb\n'), /Duplicate example: npc\.say/);
  assert.throws(() => parseExamples('npc', '## say\n\n## walk_to\n\ntext\n'), /Empty example: npc\.say/);
  assert.throws(() => parseExamples('npc', ''), /npc: no examples/);
});

test('an example is written under its function, after the help text and before the parameters', () => {
  const page = renderLua(sample(), { examples: { npc: parseExamples('npc', examplesText) } }).get('npc.md');
  const section = page.slice(page.indexOf('### walk_to'), page.length);
  const help = section.indexOf('Second line.</p>'), example = section.indexOf('Call it on every think:'), table = section.indexOf('<table>');
  assert.ok(help > 0 && help < example && example < table);
  assert.ok(section.includes('```lua\nlocal state = npc.walk_to(serial, 1434, 1699)\n```'));
  // A function without an example is rendered as before.
  assert.equal(renderLua(sample(), { examples: {} }).get('log.md'), renderLua(sample()).get('log.md'));
});

test('render rejects examples for a module or a function the server does not publish', () => {
  assert.throws(() => renderLua(sample(), { examples: { ghost: new Map([['x', 'text']]) } }), /Examples for an unknown module: ghost/);
  assert.throws(() => renderLua(sample(), { examples: { npc: new Map([['fly', 'text']]) } }), /Example for an unknown function: npc\.fly/);
});

test('an examples file is rejected for a code block left open', () => {
  assert.throws(() => parseExamples('npc', '## say\n\n```lua\nnpc.say(serial, "hi")\n\n## walk_to\n\ntext\n'), /npc: unclosed code block/);
});

test('an examples file is rejected for a heading that is not a function section', () => {
  assert.throws(() => parseExamples('npc', '## say\n\n### walk_to\n\ntext\n'), /npc\.say: a heading inside an example/);
  assert.throws(() => parseExamples('npc', '# Examples\n\n## say\n\ntext\n'), /npc: text before the first function heading/);
  // A comment inside a code block is not a heading.
  assert.deepEqual([...parseExamples('npc', '## say\n\n```lua\n# not a heading\n```\n').keys()], ['say']);
});

test('the example files of a directory are read by module, and a missing directory has none', async () => {
  const root = await mkdtemp(path.join(tmpdir(), 'moongate-lua-examples-'));
  try {
    await writeFile(path.join(root, 'npc.md'), '## say\n\ntext\n');
    await writeFile(path.join(root, 'notes.txt'), 'not an examples file');
    const examples = await readExamples(root);
    assert.deepEqual(Object.keys(examples), ['npc']);
    assert.equal(examples.npc.get('say'), 'text');
    assert.deepEqual(await readExamples(path.join(root, 'missing')), {});
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
