import assert from 'node:assert/strict';
import test from 'node:test';
import { createHash } from 'node:crypto';
import { parseTranslation, localizedPath, headingAliases } from '../scripts/translations.mjs';
const source = '# Welcome\n\n## First step\n\nRun `foo`.\n\n```sh\nfoo --bar\n```\n';
const translated = '# Benvenuto\n\n## Primo passo\n\nEsegui `foo`.\n\n```sh\nfoo --bar\n```\n';
const wrap = (body = translated, hash = createHash('sha256').update(source).digest('hex')) => `<!-- translation: ${JSON.stringify({ sourceHash: hash, title: 'Benvenuto' })} -->\n\n${body}`;
test('translation metadata is stripped and the matching source revision is accepted', () => {
  assert.deepEqual(parseTranslation(wrap(), source), { title: 'Benvenuto', markdown: translated, stale: false });
});
test('source changes mark translations stale without pretending they are current', () => {
  assert.equal(parseTranslation(wrap(), source + '\nNew text.').stale, true);
});
for (const [name, text] of [['metadata', translated], ['code', wrap(translated.replace('foo --bar', 'foo --baz'))], ['headings', wrap(translated.replace('## Primo passo', '### Primo passo'))]]) {
  test(`invalid translation ${name} is rejected`, () => assert.throws(() => parseTranslation(text, source)));
}
test('Italian links localize authored routes and retain English references and assets', () => {
  const entries = [{ slug: 'start/overview' }, { slug: 'changelog', englishOnly: true }];
  for (const [input, expected] of [['/start/overview/#welcome','/it/start/overview/#welcome'], ['/changelog/','/changelog/'], ['/lua/book/','/lua/book/'], ['/packets/','/packets/'], ['/generated/images/a.png','/generated/images/a.png'], ['/','/it/']]) {
    assert.equal(localizedPath(input, 'it', entries), expected);
  }
  assert.equal(localizedPath('/start/overview/', undefined, entries), '/start/overview/');
});
test('heading aliases retain English fragments including duplicate headings', () => {
  assert.deepEqual(headingAliases('# Title\n## Usage\n## Usage\n', '# Titolo\n## Uso\n## Uso\n'), ['title', 'usage', 'usage-1']);
});

test('translation validation preserves HTML destinations, explicit IDs and table shape', () => {
  const source = '# Guide\n\n<a id="example" href="/lua/">API</a>\n\n| Key | Meaning |\n| --- | --- |\n| foo | A value |\n';
  const body = '# Guida\n\n<a id="example" href="/lua/">API</a>\n\n| Chiave | Significato |\n| --- | --- |\n| foo | Un valore |\n';
  const wrap = text => `<!-- translation: ${JSON.stringify({ title: 'Guida', sourceHash: createHash('sha256').update(source).digest('hex') })} -->\n\n${text}`;
  assert.equal(parseTranslation(wrap(body), source).stale, false);
  assert.throws(() => parseTranslation(wrap(body.replace('href="/lua/"', 'href="/it/lua/"')), source), /HTML/);
  assert.throws(() => parseTranslation(wrap(body.replace('id="example"', 'id="esempio"')), source), /HTML/);
  assert.throws(() => parseTranslation(wrap(body.replace('| foo | Un valore |\n', '')), source), /table/);
});

test('translation validation preserves inline commands and API identifiers', () => {
  assert.throws(() => parseTranslation(wrap(translated.replace('Esegui `foo`', 'Esegui `bar`')), source), /identifiers/);
});

test('translation validation catches a missing prose paragraph', () => {
  const expanded = source + '\nAdditional instructions.\n';
  const hash = createHash('sha256').update(expanded).digest('hex');
  assert.throws(() => parseTranslation(wrap(translated, hash), expanded), /blocks/);
});

test('absolute links to authored pages on the documentation domain stay in Italian', () => {
  const entries = [{ slug: 'server/persistence' }];
  assert.equal(localizedPath('https://moongate.sh/server/persistence/#save', 'it', entries), '/it/server/persistence/#save');
  assert.equal(localizedPath('https://moongate.sh/', 'it', entries), '/it/');
  assert.equal(localizedPath('https://moongate.sh/lua/', 'it', entries), 'https://moongate.sh/lua/');
  assert.equal(localizedPath('https://example.com/server/persistence/', 'it', entries), 'https://example.com/server/persistence/');
});
