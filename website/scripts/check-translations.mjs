import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { contentEntries } from '../content-manifest.mjs';
import { repositoryFile } from './document-links.mjs';
import { parseTranslation } from './translations.mjs';

export async function checkTranslations({ repositoryRoot, websiteRoot, entries = contentEntries }) {
  const directory = path.join(websiteRoot, 'translations/it');
  const errors = [], expected = new Set();
  for (const entry of entries) {
    if (entry.englishOnly) continue;
    expected.add(`${entry.slug}.md`);
    try {
      const source = await readFile(repositoryFile(repositoryRoot, entry.source), 'utf8');
      const file = repositoryFile(repositoryRoot, path.relative(repositoryRoot, path.join(directory, `${entry.slug}.md`)));
      if (parseTranslation(await readFile(file, 'utf8'), source).stale) errors.push(`Stale translation: it/${entry.slug}`);
    } catch (error) { errors.push(`it/${entry.slug}: ${error.message}`); }
  }
  try {
    for (const file of await readdir(directory, { recursive: true, withFileTypes: true })) {
      if (file.isDirectory()) continue;
      const relative = path.relative(directory, path.join(file.parentPath, file.name)).split(path.sep).join('/');
      if (!expected.has(relative)) errors.push(`Unexpected translation: it/${relative}`);
    }
  } catch (error) { if (error.code !== 'ENOENT') throw error; }
  return errors;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const websiteRoot = fileURLToPath(new URL('../', import.meta.url));
  const errors = await checkTranslations({ repositoryRoot: path.resolve(websiteRoot, '..'), websiteRoot });
  if (errors.length) { console.error(errors.join('\n')); process.exitCode = 1; }
  else console.log(`Italian translations verified (${contentEntries.filter(entry => !entry.englishOnly).length} pages).`);
}
