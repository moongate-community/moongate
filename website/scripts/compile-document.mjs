import { remark } from 'remark';
import remarkGfm from 'remark-gfm';
import { visit } from 'unist-util-visit';
import { parseFragment } from 'parse5';
import { resolveDocumentUrl } from './document-links.mjs';

// Edit only source spans: serializing an isolated inline opening tag would close it.
function transformHtml(html, resolve, removeTitle) {
  const fragment = parseFragment(html, { sourceCodeLocationInfo: true });
  const edits = [];
  const walk = (node, root = false) => {
    const location = node.sourceCodeLocation;
    if (root && node.tagName === 'h1' && removeTitle()) {
      edits.push({ start: location.startOffset, end: location.endOffset, text: '' });
      return;
    }
    for (const attr of node.attrs ?? []) {
      if (attr.name !== 'href' && attr.name !== 'src') continue;
      const span = location?.attrs?.[attr.name];
      if (!span) continue;
      const value = resolve(attr.value).replaceAll('&', '&amp;').replaceAll('"', '&quot;');
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
  tree.children = tree.children.filter(node => !(node.type === 'heading' && node.depth === 1 && removeTitle()));
  visit(tree, node => {
    if (['link', 'image', 'definition'].includes(node.type)) node.url = resolve(node.url);
    if (node.type === 'html') node.value = transformHtml(node.value, resolve, removeTitle);
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
