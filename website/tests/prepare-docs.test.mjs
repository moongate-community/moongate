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
