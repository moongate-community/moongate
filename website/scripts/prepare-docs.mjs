import { mkdir, mkdtemp, readFile, writeFile, copyFile, rename, rm, lstat, realpath } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { contentEntries } from '../content-manifest.mjs';
import { repositoryFile } from './document-links.mjs';
import { compileDocument } from './compile-document.mjs';

async function exists(file) {
  try { await lstat(file); return true; }
  catch (error) { if (error.code === 'ENOENT') return false; throw error; }
}

// Reject symlinked output parents rather than allowing generation outside website/.
async function ensureOwnedParent(websiteRoot, destination) {
  let current = websiteRoot;
  for (const segment of path.relative(websiteRoot, path.dirname(destination)).split(path.sep)) {
    current = path.join(current, segment);
    if (await exists(current)) {
      if ((await lstat(current)).isSymbolicLink()) throw new Error(`Symlinked output directory: ${current}`);
    } else await mkdir(current);
  }
  if (await exists(destination) && (await lstat(destination)).isSymbolicLink()) {
    throw new Error(`Symlinked generated directory: ${destination}`);
  }
}

export async function prepareDocs({ repositoryRoot, websiteRoot, sourceRef = 'develop', entries = contentEntries }) {
  const slugs = new Set();
  const sources = new Set();
  for (const entry of entries) {
    if (!/^[a-z0-9]+(?:[/-][a-z0-9]+)*$/.test(entry.slug) || entry.slug === '404') {
      throw new Error(`Invalid documentation slug: ${entry.slug}`);
    }
    if (slugs.has(entry.slug)) throw new Error(`Duplicate documentation slug: ${entry.slug}`);
    if (sources.has(entry.source)) throw new Error(`Duplicate documentation source: ${entry.source}`);
    slugs.add(entry.slug);
    sources.add(entry.source);
    repositoryFile(repositoryRoot, entry.source);
  }
  const assets = new Map();
  // Compile in memory before touching previous generated output.
  const pages = [];
  for (const entry of entries) {
    const markdown = await readFile(repositoryFile(repositoryRoot, entry.source), 'utf8');
    pages.push({ entry, markdown: compileDocument(entry, markdown, { repositoryRoot, sourceRef, entries, assets }) });
  }
  await mkdir(websiteRoot, { recursive: true });
  websiteRoot = await realpath(websiteRoot);
  const destinations = [path.join(websiteRoot, 'src/content/docs/generated'), path.join(websiteRoot, 'public/generated')];
  for (const destination of destinations) await ensureOwnedParent(websiteRoot, destination);
  const stage = await mkdtemp(path.join(websiteRoot, '.docs-stage-'));
  const staged = [path.join(stage, 'pages'), path.join(stage, 'assets')];
  const backups = destinations.map((_, index) => path.join(stage, `backup-${index}`));
  const backedUp = [], published = [];
  try {
    for (const directory of staged) await mkdir(directory);
    for (const { entry, markdown } of pages) {
      const file = path.join(staged[0], `${entry.slug}.md`);
      await mkdir(path.dirname(file), { recursive: true });
      await writeFile(file, markdown);
    }
    for (const source of assets.keys()) {
      const destination = path.join(staged[1], source);
      await mkdir(path.dirname(destination), { recursive: true });
      await copyFile(repositoryFile(repositoryRoot, source), destination);
    }
    // Both trees are ready. Keep backups until publication succeeds so failures roll back.
    try {
      for (let i = 0; i < destinations.length; i++) {
        if (await exists(destinations[i])) {
          await rename(destinations[i], backups[i]);
          backedUp.push(i);
        }
        await rename(staged[i], destinations[i]);
        published.push(i);
      }
    } catch (error) {
      for (const i of published.reverse()) await rm(destinations[i], { recursive: true, force: true });
      for (const i of backedUp.reverse()) await rename(backups[i], destinations[i]);
      throw error;
    }
  } finally {
    await rm(stage, { recursive: true, force: true });
  }
  return { pages: pages.length, assets: assets.size };
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const websiteRoot = fileURLToPath(new URL('../', import.meta.url));
  const result = await prepareDocs({
    repositoryRoot: path.resolve(websiteRoot, '..'), websiteRoot,
    sourceRef: process.env.MOONGATE_DOCS_REF || 'develop',
  });
  console.log(`Imported ${result.pages} pages and ${result.assets} assets.`);
}
