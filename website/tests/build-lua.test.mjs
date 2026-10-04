import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { renderLua, writePages } from '../scripts/build-lua.mjs';

const parameter = (name, type = 'integer', optional = false, fallback = null) => ({ name, type, optional, default: fallback });
const fn = (name, parameters = [], returns = null, help = `Does ${name}.`) => ({ name, help, parameters, returns });
const module = (name, functions = [], constants = [], description = `The ${name} module.`) => ({
  name, description, source: `src/Moongate.Server.Ultima/Modules/${name}.cs`, functions, constants,
});
const sample = () => ({
  modules: [
    module('npc', [
      fn('walk_to', [parameter('serial'), parameter('z', 'integer', true), parameter('running', 'boolean', true, 'false')], 'string?',
        'Walks <b>fast</b>, answers a|b or `nil`.\nSecond line.'),
      fn('say', [parameter('...', 'any')]),
      fn('step', [parameter('direction', 'DirectionType|string'), parameter('again', 'DirectionType?', true)], 'DirectionType'),
    ]),
    module('log', [], [{ name: 'LEVEL_DEBUG', type: 'integer', value: '1', help: 'The a|b level.' }]),
  ],
  enums: [{ name: 'DirectionType', members: [{ name: 'North', value: 0 }, { name: 'Right', value: 1 }] }],
});

test('render writes an overview, one page per module and the enums', () => {
  const pages = renderLua(sample());
  assert.deepEqual([...pages.keys()].sort(), ['enums.md', 'index.md', 'log.md', 'npc.md']);
  for (const page of pages.values()) assert.match(page, /^---\ntitle: "/);
});

test('a module page gives each function a heading, its signature, its help and its parameters', () => {
  const page = renderLua(sample(), { sourceRef: 'v1.2.3' }).get('npc.md');
  assert.match(page, /^title: "npc"$/m);
  assert.match(page, /^The npc module\.$/m);
  assert.ok(page.includes('[`src/Moongate.Server.Ultima/Modules/npc.cs`](https://github.com/moongate-community/moongate/blob/v1.2.3/src/Moongate.Server.Ultima/Modules/npc.cs)'));
  assert.ok(page.includes('### walk_to\n\n```lua\nnpc.walk_to(serial, z?, running?) -> string?\n```'));
  assert.ok(page.includes('| `serial` | `integer` | |'));
  assert.ok(page.includes('| `z` (optional) | `integer` | `nil` |'));
  assert.ok(page.includes('| `running` (optional) | `boolean` | `false` |'));
  // Functions are listed by name, whatever the dump's order.
  assert.ok(page.indexOf('### say') < page.indexOf('### step') && page.indexOf('### step') < page.indexOf('### walk_to'));
});

test('help text keeps its lines and cannot open an HTML tag', () => {
  const page = renderLua(sample()).get('npc.md');
  assert.ok(page.includes('Walks &lt;b>fast&lt;/b>, answers a|b or `nil`.\nSecond line.'));
});

test('varargs show as ... and a function that returns nothing has no arrow', () => {
  const page = renderLua(sample()).get('npc.md');
  assert.ok(page.includes('```lua\nnpc.say(...)\n```'));
  assert.ok(page.includes('| `...` | `any` | |'));
});

test('an enum type links to the enums page and its pipe stays inside the table cell', () => {
  const page = renderLua(sample()).get('npc.md');
  assert.ok(page.includes('| `direction` | [`DirectionType`](/lua/enums/#directiontype)\\|`string` | |'));
  assert.ok(page.includes('| `again` (optional) | [`DirectionType?`](/lua/enums/#directiontype) | `nil` |'));
  assert.ok(page.includes('npc.step(direction, again?) -> DirectionType'));
});

test('a section without rows is left out', () => {
  const pages = renderLua(sample());
  assert.ok(pages.get('log.md').includes('## Constants'));
  assert.ok(!pages.get('log.md').includes('## Functions'));
  assert.ok(pages.get('log.md').includes('| `LEVEL_DEBUG` | `integer` | `1` | The a\\|b level. |'));
  assert.ok(!pages.get('npc.md').includes('## Constants'));
});

test('the overview lists the modules by name, the globals and the enums', () => {
  const page = renderLua(sample()).get('index.md');
  assert.ok(page.indexOf('[`log`](/lua/log/)') < page.indexOf('[`npc`](/lua/npc/)'));
  assert.ok(page.includes('| [`npc`](/lua/npc/) | The npc module. |'));
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
  badModule.modules[1].name = 'Log';
  assert.throws(() => renderLua(badModule), /Invalid module name: Log/);
  const badFunction = sample();
  badFunction.modules[0].functions[0].name = 'walk-to';
  assert.throws(() => renderLua(badFunction), /Invalid function name: npc\.walk-to/);
  const reserved = sample();
  reserved.modules[0].functions[0].name = 'functions';
  assert.throws(() => renderLua(reserved), /Invalid function name: npc\.functions/);
  const repeatedModule = sample();
  repeatedModule.modules[1].name = 'npc';
  assert.throws(() => renderLua(repeatedModule), /Duplicate module: npc/);
  const repeatedFunction = sample();
  repeatedFunction.modules[0].functions[1].name = 'step';
  assert.throws(() => renderLua(repeatedFunction), /Duplicate function: npc\.step/);
  const badConstant = sample();
  badConstant.modules[1].constants[0].name = 'LEVEL DEBUG';
  assert.throws(() => renderLua(badConstant), /Invalid constant name: log\.LEVEL DEBUG/);
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
    assert.deepEqual((await readdir(directory)).sort(), ['enums.md', 'index.md', 'log.md', 'npc.md']);
    assert.match(await readFile(path.join(directory, 'npc.md'), 'utf8'), /### walk_to/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
