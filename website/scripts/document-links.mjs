import { realpathSync, statSync } from 'node:fs';
import path from 'node:path';

const repositoryUrl = 'https://github.com/moongate-community/moongate';
const imageExtensions = new Set(['.png', '.jpg', '.jpeg', '.gif', '.svg', '.webp', '.avif', '.ico']);
const encodePath = value => value.split('/').map(encodeURIComponent).join('/');

// Follow symlinks before reading so a repository link cannot expose outside files.
export function repositoryFile(repositoryRoot, relativePath) {
  const root = realpathSync(repositoryRoot);
  const candidate = path.resolve(root, relativePath);
  const contained = file => file.startsWith(`${root}${path.sep}`);
  if (!contained(candidate)) throw new Error(`Path escapes repository: ${relativePath}`);
  const real = realpathSync(candidate);
  if (!contained(real) || !statSync(real).isFile()) throw new Error(`Not a repository file: ${relativePath}`);
  return real;
}

export function resolveDocumentUrl(url, { source, repositoryRoot, sourceRef, entries, assets }) {
  if (!url || url.startsWith('#') || url.startsWith('?') || url.startsWith('/moongate/')) return url;
  let local = url;
  let absoluteRepositoryPath = false;
  if (/^(?:[a-z][a-z\d+.-]*:|\/\/)/i.test(url)) {
    const prefixes = [
      `${repositoryUrl}/blob/`,
      'https://raw.githubusercontent.com/moongate-community/moongate/',
    ];
    const prefix = prefixes.find(value => url.startsWith(value));
    if (!prefix) return url;
    const suffix = url.slice(prefix.length);
    const refs = [...new Set(['develop', 'main', sourceRef])];
    const ref = refs.find(value => suffix.startsWith(`${encodeURIComponent(value)}/`));
    // Links deliberately pointing at other historical revisions stay external.
    if (!ref) return url;
    local = suffix.slice(encodeURIComponent(ref).length + 1);
    absoluteRepositoryPath = true;
  }
  try {
    const [, pathname, suffix] = local.match(/^([^?#]*)(.*)$/);
    const decoded = decodeURIComponent(pathname);
    if (decoded.startsWith('/') || decoded.includes('\\')) throw new Error('Unsupported absolute path');
    const target = path.posix.normalize(absoluteRepositoryPath ? decoded : path.posix.join(path.posix.dirname(source), decoded));
    repositoryFile(repositoryRoot, target);
    const entry = entries.find(value => value.source === target);
    if (entry) return `/moongate/${entry.slug}/${suffix}`;
    if (imageExtensions.has(path.posix.extname(target).toLowerCase())) {
      const assetUrl = `/moongate/generated/${encodePath(target)}`;
      assets.set(target, assetUrl);
      return `${assetUrl}${suffix}`;
    }
    return `${repositoryUrl}/blob/${encodeURIComponent(sourceRef)}/${encodePath(target)}${suffix}`;
  } catch (error) {
    throw new Error(`${source}: cannot resolve ${JSON.stringify(url)}: ${error.message}`, { cause: error });
  }
}
