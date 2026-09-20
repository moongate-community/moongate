import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { validateSite } from '../scripts/check-links.mjs';

async function fixture(t, home) {
  const directory = await mkdtemp(join(tmpdir(), 'docs-site-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  await mkdir(join(directory, 'libraries/api'), { recursive: true });
  await writeFile(join(directory, 'index.html'), home);
  await writeFile(join(directory, 'libraries/api/index.html'), '<h2 id="wire-format">Wire</h2><a name="legacy"></a><h2 id="café">Cafe</h2>');
  await writeFile(join(directory, 'logo.png'), 'image');
  return { directory, site: 'https://moongate-community.github.io', basePath: '/moongate/', expectedSlugs: ['libraries/api'] };
}

test('accepts local routes, fragments, entities, encoded anchors, and external destinations', async t => {
  const options = await fixture(t, `<a href="/moongate/libraries/api/?a=1&amp;b=2#wire-format">API</a>
    <a href="libraries/api/#caf%C3%A9">Cafe</a><a href="libraries/api/#legacy">Legacy</a>
    <a href="libraries/api/#wire-format:~:text=Wire">Text</a><a href="libraries/api/#:~:text=Wire">Text only</a>
    <a href="https://example.com/missing">External</a><a href="mailto:dev@example.com">Mail</a>
    <a href="https://github.com/moongate-community/moongate/blob/v0.4.0/README.md">Source</a>
    <img src="/moongate/logo.png" srcset="/moongate/logo.png 1x, /moongate/logo.png 2x">`);
  await writeFile(join(options.directory, '404.html'), '<a href="/moongate/">Home</a>');
  assert.deepEqual(await validateSite(options), []);
});

for (const [name, html, match] of [
  ['missing fragment', '<a href="/moongate/libraries/api/#missing">API</a>', 'missing'],
  ['missing image', '<img src="/moongate/absent.png">', 'absent.png'],
  ['missing base prefix', '<a href="/libraries/api/">API</a>', 'base path'],
  ['missing srcset image', '<img srcset="/moongate/logo.png 1x, /moongate/missing.png 2x">', 'missing.png'],
  ['encoded traversal', '<a href="/moongate/%2e%2e%2foutside">Escape</a>', 'outside'],
]) {
  test(`reports ${name} with the source path`, async t => {
    const errors = await validateSite(await fixture(t, html));
    assert(errors.some(error => error.includes('index.html') && error.includes(match)), errors.join('\n'));
  });
}

test('reports missing expected routes even if no page links to them', async t => {
  const options = await fixture(t, 'Home');
  options.expectedSlugs.push('server/missing');
  assert((await validateSite(options)).some(error => error.includes('server/missing')));
});

test('resolves relative links from nested pages and from 404.html', async t => {
  const options = await fixture(t, 'Home');
  await writeFile(join(options.directory, 'libraries/api/index.html'), '<a href="../../">Home</a>');
  await writeFile(join(options.directory, '404.html'), '<a href="libraries/api/">API</a><img src="missing.png">');
  const errors = await validateSite(options);
  assert.equal(errors.length, 1);
  assert(errors[0].includes('404.html') && errors[0].includes('missing.png'));
});

test('does not mistake the payload of a data srcset URL for a local file', async t => {
  const options = await fixture(t, '<img srcset="data:image/png;base64,aGVsbG8= 1x, /moongate/logo.png 2x">');
  assert.deepEqual(await validateSite(options), []);
});


test('validates custom-domain root links and rejects stale project-prefixed links', async t => {
  const options = await fixture(t, '<a href="https://moongate.sh/libraries/api/#wire-format">API</a><img src="/logo.png">');
  options.site = 'https://moongate.sh';
  options.basePath = '/';
  assert.deepEqual(await validateSite(options), []);
  await writeFile(join(options.directory, 'index.html'), '<a href="/moongate/libraries/api/">Stale</a>');
  const errors = await validateSite(options);
  assert(errors.some(error => error.includes('/moongate/libraries/api/')));
});
