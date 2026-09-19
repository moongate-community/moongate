import { remark } from 'remark';
import remarkGfm from 'remark-gfm';
import { visit } from 'unist-util-visit';
import { parseFragment } from 'parse5';
import { slug } from 'github-slugger';
import { toString } from 'mdast-util-to-string';
import { rewriteSrcset } from './srcset.mjs';
import { resolveDocumentUrl } from './document-links.mjs';

const escapeAttribute = value => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;');

// Edit only source spans: serializing an isolated inline opening tag would close it.
function transformHtml(html, resolve, removeTitle) {
  const fragment = parseFragment(html, { sourceCodeLocationInfo: true });
  const edits = [];
  const walk = (node, root = false) => {
    const location = node.sourceCodeLocation;
    if (root && node.tagName === 'h1' && removeTitle()) {
      const id = node.attrs.find(attr => attr.name === 'id')?.value;
      edits.push({ start: location.startOffset, end: location.endOffset, text: id ? `<a id="${escapeAttribute(id)}"></a>` : '' });
      return;
    }
    for (const attr of node.attrs ?? []) {
      if (!['href', 'src', 'srcset'].includes(attr.name)) continue;
      const span = location?.attrs?.[attr.name];
      if (!span) continue;
      const value = escapeAttribute(attr.name === 'srcset' ? rewriteSrcset(attr.value, resolve) : resolve(attr.value));
      edits.push({ start: span.startOffset, end: span.endOffset, text: `${attr.name}="${value}"` });
    }
    for (const child of node.childNodes ?? []) walk(child);
  };
  for (const node of fragment.childNodes) walk(node, true);
  for (const edit of edits.sort((a, b) => b.start - a.start)) html = html.slice(0, edit.start) + edit.text + html.slice(edit.end);
  return html;
}

export function compileDocument(entry, markdown, context) {
  const processor = remark().use(remarkGfm);
  const tree = processor.parse(markdown);
  let removedTitle = false;
  const removeTitle = () => {
    if (removedTitle) return false;
    removedTitle = true;
    return true;
  };
  const resolve = url => resolveDocumentUrl(url, { ...context, source: entry.source });
  visit(tree, (node, index, parent) => {
    if (parent === tree && node.type === 'heading' && node.depth === 1 && removeTitle()) {
      const id = slug(toString(node));
      node.type = 'html';
      node.value = `<a id="${escapeAttribute(id)}"></a>`;
      delete node.children;
      delete node.depth;
    }
    if (['link', 'image', 'definition'].includes(node.type)) node.url = resolve(node.url);
    if (node.type === 'html') node.value = transformHtml(node.value, resolve, parent === tree ? removeTitle : () => false);
  });
  const header = [
    '---',
    `title: ${JSON.stringify(entry.title)}`,
    `slug: ${JSON.stringify(entry.slug)}`,
    `editUrl: ${JSON.stringify(`https://github.com/moongate-community/moongate/edit/develop/${entry.source}`)}`,
    '---', '',
  ].join('\n');
  return `${header}\n${processor.stringify(tree)}`;
}
