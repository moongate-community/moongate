import { parseFragment } from 'parse5';
import { createHash } from 'node:crypto';
import { remark } from 'remark';
import remarkGfm from 'remark-gfm';
import { visit } from 'unist-util-visit';
import GithubSlugger from 'github-slugger';
import { toString } from 'mdast-util-to-string';

function structure(markdown) {
  const headings = [], code = [], destinations = [], HTML = [], tables = [], identifiers = [], blocks = [];
  const htmlAttributes = node => {
    for (const attr of node.attrs ?? []) {
      if (['id', 'href', 'src', 'srcset'].includes(attr.name) || (node.tagName === 'a' && attr.name === 'name')) {
        HTML.push([node.tagName, attr.name, attr.value]);
      }
    }
    for (const child of node.childNodes ?? []) htmlAttributes(child);
  };
  visit(remark().use(remarkGfm).parse(markdown), node => {
    if (['paragraph', 'listItem', 'blockquote', 'tableRow'].includes(node.type)) blocks.push(node.type);
    if (node.type === 'heading') headings.push({ depth: node.depth, text: toString(node) });
    if (node.type === 'code') code.push(markdown.slice(node.position.start.offset, node.position.end.offset));
    if (node.type === 'inlineCode') identifiers.push(node.value);
    if (node.type === 'html') htmlAttributes(parseFragment(node.value));
    if (node.type === 'table') tables.push([node.align, node.children.map(row => row.children.length)]);
    if (['link', 'image', 'definition'].includes(node.type)) destinations.push(node.url);
  });
  return { headings, code, destinations, HTML, tables, identifiers: identifiers.sort(), blocks };
}

export function parseTranslation(text, source) {
  const match = text.match(/^<!-- translation: (\{[^\n]*\}) -->\r?\n\r?\n/);
  if (!match) throw new Error('Missing translation metadata');
  const metadata = JSON.parse(match[1]);
  if (!/^[a-f0-9]{64}$/.test(metadata.sourceHash) || typeof metadata.title !== 'string' || !metadata.title.trim()) {
    throw new Error('Invalid translation metadata');
  }
  const markdown = text.slice(match[0].length);
  const stale = metadata.sourceHash !== createHash('sha256').update(source).digest('hex');
  // A stale file belongs to an earlier structure; it is never published as current.
  if (!stale) {
    const original = structure(source), translated = structure(markdown);
    for (const key of ['code', 'destinations', 'HTML', 'tables', 'identifiers', 'blocks']) {
      if (JSON.stringify(original[key]) !== JSON.stringify(translated[key])) throw new Error(`Translation changed ${key}`);
    }
    if (JSON.stringify(original.headings.map(h => h.depth)) !== JSON.stringify(translated.headings.map(h => h.depth))) {
      throw new Error('Translation changed heading structure');
    }
  }
  return { title: metadata.title, markdown, stale };
}

export function headingAliases(source, translated) {
  const original = new GithubSlugger(), localized = new GithubSlugger();
  const headings = structure(translated).headings;
  return structure(source).headings.map((heading, i) => {
    const id = original.slug(heading.text);
    return id === localized.slug(headings[i].text) ? null : id;
  });
}

export function localizedPath(url, locale, entries) {
  if (!url || locale !== 'it' || !url.startsWith('/') || url.startsWith('//')) return url;
  const [, pathname, suffix] = url.match(/^([^?#]*)(.*)$/);
  const slug = pathname.replace(/^\/|\/$/g, '');
  if (!slug || entries.some(entry => entry.slug === slug && !entry.englishOnly)) return `/it${pathname}${suffix}`;
  return url;
}

export function isEnglishOnlyPath(pathname, entries) {
  const slug = pathname.replace(/^\/(?:it\/)?/, '').replace(/\/$/, '');
  return slug === 'packets' || slug === 'lua' || slug.startsWith('lua/') || entries.some(entry => entry.englishOnly && entry.slug === slug);
}
