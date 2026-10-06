import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdtemp, mkdir, writeFile, readFile, rm, access } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { prepareDocs } from '../scripts/prepare-docs.mjs';

async function fixture(t) {
  const repositoryRoot = await mkdtemp(join(tmpdir(), 'docs-prepare-'));
  t.after(() => rm(repositoryRoot, { recursive: true, force: true }));
  const websiteRoot = join(repositoryRoot, 'website');
  const generated = join(websiteRoot, 'src/content/docs/generated');
  await mkdir(generated, { recursive: true });
  await writeFile(join(generated, 'stale.md'), 'last successful build');
  await writeFile(join(websiteRoot, 'src/content/docs/index.mdx'), 'authored home');
  await mkdir(join(repositoryRoot, 'images'));
  await writeFile(join(repositoryRoot, 'images/logo.png'), 'image bytes');
  await writeFile(join(repositoryRoot, 'README.md'), '# Title\n\n![Logo](images/logo.png)\n');
  await mkdir(join(repositoryRoot, 'scripts'));
  await writeFile(join(repositoryRoot, 'scripts/install.sh'), '#!/bin/sh\necho install\n');
  const entries = [{ source: 'README.md', slug: 'start/overview', title: 'Overview' }];
  return { repositoryRoot, websiteRoot, entries, sourceRef: 'v0.4.0', generated };
}

test('regeneration replaces stale generated pages, copies assets, and preserves authored files', async t => {
  const options = await fixture(t);
  const before = await readFile(join(options.repositoryRoot, 'README.md'));
  assert.deepEqual(await prepareDocs(options), { pages: 1, assets: 1 });
  await assert.rejects(access(join(options.generated, 'stale.md')));
  assert.match(await readFile(join(options.generated, 'start/overview.md'), 'utf8'), /slug: "start\/overview"/);
  assert.equal(await readFile(join(options.websiteRoot, 'public/generated/images/logo.png'), 'utf8'), 'image bytes');
  assert.equal(await readFile(join(options.websiteRoot, 'public/install.sh'), 'utf8'), '#!/bin/sh\necho install\n');
  assert.deepEqual(await readFile(join(options.repositoryRoot, 'README.md')), before);
  assert.equal(await readFile(join(options.websiteRoot, 'src/content/docs/index.mdx'), 'utf8'), 'authored home');
});

for (const defect of ['missing source', 'duplicate slug', 'invalid slug', 'unresolved link', 'source escape', 'missing installer']) {
  test(`${defect} fails without replacing the previous build or authored home`, async t => {
    const options = await fixture(t);
    if (defect === 'missing source') options.entries.push({ source: 'absent.md', slug: 'missing' });
    if (defect === 'duplicate slug') options.entries.push({ ...options.entries[0] });
    if (defect === 'invalid slug') options.entries[0].slug = '../escape';
    if (defect === 'source escape') options.entries[0].source = '../escape.md';
    if (defect === 'unresolved link') await writeFile(join(options.repositoryRoot, 'README.md'), '[Bad](missing.md)');
    if (defect === 'missing installer') await rm(join(options.repositoryRoot, 'scripts/install.sh'));
    await assert.rejects(prepareDocs(options));
    assert.equal(await readFile(join(options.generated, 'stale.md'), 'utf8'), 'last successful build');
    assert.equal(await readFile(join(options.websiteRoot, 'src/content/docs/index.mdx'), 'utf8'), 'authored home');
  });
}

async function coverageFixture(t) {
  const options = await fixture(t);
  await writeFile(join(options.repositoryRoot, 'coverage.md'), '# Test coverage\n\nIntro.\n\n<!-- coverage-summary -->\n');
  options.entries.push({ source: 'coverage.md', slug: 'contributing/test-coverage', title: 'Test coverage' });
  const report = join(options.repositoryRoot, 'report');
  await mkdir(join(report, 'results/run'), { recursive: true });
  await writeFile(join(report, 'Summary.md'), '# Summary - Moongate\n\n|**Assembly**|**Line coverage**|\n|:---|---:|\n|**Moongate.Core**|**92.7%**|\n');
  await writeFile(join(report, 'Cobertura.xml'), '<?xml version="1.0"?>\n<coverage line-rate="0.657" timestamp="1790517600">\n</coverage>\n');
  await writeFile(join(report, 'index.html'), '<html>report</html>');
  await writeFile(join(report, 'results/run/coverage.cobertura.xml'), 'raw');
  return { ...options, coverage: { directory: report, commit: 'abc1234' } };
}

const coveragePage = options => readFile(join(options.generated, 'contributing/test-coverage.md'), 'utf8');

test('a coverage report fills the coverage page and is published under /coverage/', async t => {
  const options = await coverageFixture(t);
  await prepareDocs(options);
  const page = await coveragePage(options);
  assert.match(page, /Moongate\.Core\*\* *\| *\*\*92\.7%/);
  assert.doesNotMatch(page, /Summary - Moongate/);
  assert.match(page, /commit `abc1234`/);
  assert.match(page, /2026-09-27/);
  assert.match(page, /\]\(\/coverage\/\)/);
  assert.equal(await readFile(join(options.websiteRoot, 'public/coverage/index.html'), 'utf8'), '<html>report</html>');
  await assert.rejects(access(join(options.websiteRoot, 'public/coverage/results')));
});

test('without a coverage report the page says so and a previous report is removed', async t => {
  const options = await coverageFixture(t);
  await prepareDocs(options);
  await rm(options.coverage.directory, { recursive: true });
  await prepareDocs(options);
  const page = await coveragePage(options);
  assert.match(page, /No coverage report was available/);
  assert.doesNotMatch(page, /\/coverage\//);
  await assert.rejects(access(join(options.websiteRoot, 'public/coverage')));
});

test('coverage insertion preserves inline marker documentation before the report placeholder', async t => {
  const options = await coverageFixture(t);
  await writeFile(join(options.repositoryRoot, 'coverage.md'),
    '# Test coverage\n\nUse `<!-- coverage-summary -->` to insert the report.\n\n<!-- coverage-summary -->\n');
  await prepareDocs(options);
  const page = await coveragePage(options);
  assert.match(page, /Use `<!-- coverage-summary -->` to insert the report\./);
  assert.match(page, /commit `abc1234`/);
  assert.equal(page.match(/<!-- coverage-summary -->/g)?.length, 1);
});

async function addTranslation(options, source, body, hash) {
  const { createHash } = await import('node:crypto');
  const file = join(options.websiteRoot, 'translations/it/start/overview.md');
  await mkdir(join(options.websiteRoot, 'translations/it/start'), { recursive: true });
  await writeFile(file, `<!-- translation: ${JSON.stringify({ sourceHash: hash ?? createHash('sha256').update(source).digest('hex'), title: 'Panoramica' })} -->\n\n${body}`);
}

test('imports Italian with original section anchors, localized links and translation edit URL', async t => {
  const options = await fixture(t);
  const source = '# Title\n\n## Installation\n\n[Next](README.md#installation)\n';
  await writeFile(join(options.repositoryRoot, 'README.md'), source);
  await addTranslation(options, source, '# Titolo\n\n## Installazione\n\n[Avanti](README.md#installation)\n');
  assert.equal((await prepareDocs(options)).pages, 2);
  const page = await readFile(join(options.generated, 'it/start/overview.md'), 'utf8');
  assert.match(page, /slug: "it\/start\/overview"/);
  assert.match(page, /id="installation"/);
  assert.match(page, /\/it\/start\/overview\/#installation/);
  assert.match(page, /edit\/develop\/website\/translations\/it\/start\/overview.md/);
  assert.doesNotMatch(page, /translation:|sourceHash/);
});
test('stale translation is reported and removed from output for English fallback', async t => {
  const options = await fixture(t);
  const source = await readFile(join(options.repositoryRoot, 'README.md'), 'utf8');
  await addTranslation(options, source, source);
  await prepareDocs(options);
  await writeFile(join(options.repositoryRoot, 'README.md'), source + '\nChanged.\n');
  const warnings = [];
  assert.equal((await prepareDocs({ ...options, warn: message => warnings.push(message) })).pages, 1);
  assert.match(warnings.join(), /Stale translation.*start\/overview/);
  await assert.rejects(access(join(options.generated, 'it/start/overview.md')));
});
test('generated references reject translation sources without changing old output', async t => {
  const options = await fixture(t);
  options.entries[0].englishOnly = true;
  const source = await readFile(join(options.repositoryRoot, 'README.md'), 'utf8');
  await addTranslation(options, source, source);
  await assert.rejects(prepareDocs(options), /English-only/);
  assert.equal(await readFile(join(options.generated, 'stale.md'), 'utf8'), 'last successful build');
});

test('strict catalog validation reports missing, stale and unexpected translations', async t => {
  const { checkTranslations } = await import('../scripts/check-translations.mjs');
  const options = await fixture(t);
  assert.match((await checkTranslations(options)).join(), /it\/start\/overview/);
  const source = await readFile(join(options.repositoryRoot, 'README.md'), 'utf8');
  await addTranslation(options, source, source);
  assert.deepEqual(await checkTranslations(options), []);
  await writeFile(join(options.repositoryRoot, 'README.md'), source + '\nChange.\n');
  assert.match((await checkTranslations(options)).join(), /Stale translation/);
  await writeFile(join(options.websiteRoot, 'translations/it/unexpected.md'), 'unexpected');
  assert.match((await checkTranslations(options)).join(), /Unexpected translation/);
});

test('invalid translation code preserves the last complete output', async t => {
  const options = await fixture(t);
  const source = '# Title\n\n```sh\nfoo\n```\n';
  await writeFile(join(options.repositoryRoot, 'README.md'), source);
  await addTranslation(options, source, source.replace('foo', 'bar'));
  await assert.rejects(prepareDocs(options), /changed code/);
  assert.equal(await readFile(join(options.generated, 'stale.md'), 'utf8'), 'last successful build');
});

test('manifest slugs cannot occupy the reserved Italian namespace', async t => {
  const options = await fixture(t);
  options.entries[0].slug = 'it/start/overview';
  await assert.rejects(prepareDocs(options), /Invalid documentation slug/);
  assert.equal(await readFile(join(options.generated, 'stale.md'), 'utf8'), 'last successful build');
});
