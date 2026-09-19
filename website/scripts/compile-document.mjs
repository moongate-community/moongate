import { remark } from 'remark';
import remarkGfm from 'remark-gfm';
import { visit } from 'unist-util-visit';
import { parseFragment, serialize } from 'parse5';
import { resolveDocumentUrl } from './document-links.mjs';

export function compileDocument(entry, markdown, context) {
  const processor = remark().use(remarkGfm);
  const tree = processor.parse(markdown);
  let removedTitle = false;
  const resolve = url => resolveDocumentUrl(url, { ...context, source: entry.source });
  tree.children = tree.children.filter(node => {
    if (!removedTitle && node.type === 'heading' && node.depth === 1) {
      removedTitle = true;
      return false;
    }
    if (node.type === 'html') {
      const fragment = parseFragment(node.value);
      fragment.childNodes = fragment.childNodes.filter(child => {
        if (!removedTitle && child.tagName === 'h1') {
          removedTitle = true;
          return false;
        }
        return true;
      });
      node.value = serialize(fragment);
    }
    return true;
  });
  visit(tree, node => {
    if (['link', 'image', 'definition'].includes(node.type)) node.url = resolve(node.url);
    if (node.type === 'html') {
      const fragment = parseFragment(node.value);
      const walk = element => {
        for (const attr of element.attrs ?? []) {
          if (attr.name === 'href' || attr.name === 'src') attr.value = resolve(attr.value);
        }
        for (const child of element.childNodes ?? []) walk(child);
      };
      walk(fragment);
      node.value = serialize(fragment);
    }
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
