import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parse } from 'parse5';
import { srcsetUrls } from './srcset.mjs';
import { contentEntries } from '../content-manifest.mjs';

async function htmlFiles(directory, prefix = '') {
  const files = [];
  for (const entry of await readdir(path.join(directory, prefix), { withFileTypes: true })) {
    const name = path.posix.join(prefix, entry.name);
    if (entry.isDirectory()) files.push(...await htmlFiles(directory, name));
    else if (entry.isFile() && name.endsWith('.html')) files.push(name);
  }
  return files;
}

function inspectHtml(html) {
  const ids = new Set(), links = [];
  const walk = node => {
    for (const { name, value } of node.attrs ?? []) {
      if (name === 'id' || (node.tagName === 'a' && name === 'name')) ids.add(value);
      if (name === 'href' || name === 'src') links.push(value);
      if (name === 'srcset') {
        for (const candidate of srcsetUrls(value)) links.push(candidate.url);
      }
    }
    for (const child of node.childNodes ?? []) walk(child);
  };
  walk(parse(html));
  return { ids, links };
}

export async function validateSite({ directory, site, basePath, expectedSlugs = [] }) {
  directory = path.resolve(directory);
  const errors = [], documents = new Map();
  for (const file of await htmlFiles(directory)) documents.set(file, inspectHtml(await readFile(path.join(directory, file), 'utf8')));
  for (const slug of ['', ...expectedSlugs]) {
    const file = slug ? `${slug}/index.html` : 'index.html';
    if (!documents.has(file)) errors.push(`Missing expected page: ${file}`);
  }
  const origin = new URL(site).origin;
  for (const [file, document] of documents) {
    const route = file === 'index.html' ? '' : file.replace(/index\.html$/, '');
    const pageUrl = new URL(`${basePath}${route}`, origin);
    for (const destination of document.links) {
      const error = message => errors.push(`${file}: ${JSON.stringify(destination)} — ${message}`);
      let target;
      try { target = new URL(destination, pageUrl); }
      catch { error('Invalid URL'); continue; }
      if (target.origin !== origin || !['http:', 'https:'].includes(target.protocol)) continue;
      if (!target.pathname.startsWith(basePath)) { error(`Missing base path ${basePath}`); continue; }
      let relative;
      try { relative = decodeURIComponent(target.pathname.slice(basePath.length)); }
      catch { error('Invalid path encoding'); continue; }
      let resolved = path.resolve(directory, relative);
      if (resolved !== directory && !resolved.startsWith(`${directory}${path.sep}`)) { error('Path escapes site directory'); continue; }
      try {
        if ((await stat(resolved)).isDirectory()) resolved = path.join(resolved, 'index.html');
        if (!(await stat(resolved)).isFile()) { error('Target is not a file'); continue; }
      } catch { error('Missing local target'); continue; }
      if (target.hash && resolved.endsWith('.html')) {
        let anchor;
        try { anchor = decodeURIComponent(target.hash.slice(1).split(':~:')[0]); }
        catch { error('Invalid fragment encoding'); continue; }
        if (anchor && !documents.get(path.relative(directory, resolved).split(path.sep).join('/'))?.ids.has(anchor)) {
          error(`Missing fragment #${anchor}`);
        }
      }
    }
  }
  return errors;
}

if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href) {
  const errors = await validateSite({
    directory: fileURLToPath(new URL('../dist/', import.meta.url)),
    site: 'https://moongate-community.github.io', basePath: '/moongate/',
    expectedSlugs: contentEntries.map(entry => entry.slug),
  });
  if (errors.length) {
    console.error(errors.join('\n'));
    process.exitCode = 1;
  } else console.log(`Local links verified (${contentEntries.length + 1} expected pages).`);
}
