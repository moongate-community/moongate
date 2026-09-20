import assert from 'node:assert/strict';
import test from 'node:test';
import { mkdtemp, mkdir, writeFile, rm, symlink } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { remark } from 'remark';
import { resolveDocumentUrl } from '../scripts/document-links.mjs';
import { compileDocument } from '../scripts/compile-document.mjs';

async function fixture(t) {
  const repositoryRoot = await mkdtemp(join(tmpdir(), 'docs-links-'));
  t.after(() => rm(repositoryRoot, { recursive: true, force: true }));
  for (const file of ['docs/guide.md', 'src/Moongate.Api/README.md', 'images/logo.png', 'scripts/source.cs']) {
    await mkdir(join(repositoryRoot, file, '..'), { recursive: true });
    await writeFile(join(repositoryRoot, file), 'content');
  }
  return { repositoryRoot, source: 'docs/guide.md', sourceRef: 'v0.4.0', assets: new Map(), entries: [
    { source: 'src/Moongate.Api/README.md', slug: 'libraries/api' },
    { source: 'docs/guide.md', slug: 'server/guide' },
  ] };
}

test('resolves local and GitHub documents, released source, anchors, and external URLs', async t => {
  const context = await fixture(t);
  for (const [input, expected] of [
    ['../src/Moongate.Api/README.md#wire-format', '/libraries/api/#wire-format'],
    ['https://github.com/moongate-community/moongate/blob/develop/docs/guide.md', '/server/guide/'],
    ['../src/Moongate.Api/README.md?q=x#wire-format', '/libraries/api/?q=x#wire-format'],
    ['../scripts/source.cs', 'https://github.com/moongate-community/moongate/blob/v0.4.0/scripts/source.cs'],
    ['#local', '#local'], ['?q=x#local', '?q=x#local'],
    ['https://example.com/README.md', 'https://example.com/README.md'], ['mailto:dev@example.com', 'mailto:dev@example.com'],
  ]) assert.equal(resolveDocumentUrl(input, context), expected, input);
});

test('copies relative and raw GitHub images under the site base', async t => {
  const context = await fixture(t);
  for (const input of ['../images/logo.png', 'https://raw.githubusercontent.com/moongate-community/moongate/develop/images/logo.png']) {
    assert.equal(resolveDocumentUrl(input, context), '/generated/images/logo.png');
  }
  assert.deepEqual([...context.assets], [['images/logo.png', '/generated/images/logo.png']]);
});

test('rejects missing paths, repository traversal, and symlinks outside the repository', async t => {
  const context = await fixture(t);
  await symlink(tmpdir(), join(context.repositoryRoot, 'outside'));
  for (const input of ['missing.md', '../../outside.md', '../outside']) {
    assert.throws(() => resolveDocumentUrl(input, context), /docs\/guide.md/);
  }
});

test('transforms Markdown references and raw HTML while preserving code and comments', async t => {
  const context = await fixture(t);
  const entry = { source: 'docs/guide.md', slug: 'server/guide', title: 'A "quoted" title' };
  const input = '# Old title\n\n[API][api]\n\n[api]: ../src/Moongate.Api/README.md#wire-format\n\n<img src="../images/logo.png" alt="logo">\n\n<!-- nuget-smoke:Program.cs -->\n\n```csharp\nvar path = "../README.md";\n```\n\n`../README.md`\n';
  const result = compileDocument(entry, input, context);
  assert.match(result, /slug: "server\/guide"/);
  assert.match(result, /title: "A \\"quoted\\" title"/);
  assert(!result.includes('# Old title'));
  assert(result.includes('/libraries/api/#wire-format'));
  assert(result.includes('src="/generated/images/logo.png"'));
  assert(result.includes('<!-- nuget-smoke:Program.cs -->'));
  const tree = remark().parse(result);
  assert.equal(tree.children.find(n => n.type === 'code').value, 'var path = "../README.md";');
  assert(result.includes('`../README.md`'));
});

test('removes the first HTML H1 without removing surrounding content', async t => {
  const context = await fixture(t);
  const result = compileDocument({ source: 'docs/guide.md', title: 'Guide', slug: 'server/guide' }, '<h1 align="center">Old</h1>\n\n<p>Keep me</p>\n', context);
  assert(!result.includes('<h1'));
  assert(result.includes('<p>Keep me</p>'));
});

test('preserves inline HTML opening and closing tags around linked text', async t => {
  const context = await fixture(t);
  const result = compileDocument({ source: 'docs/guide.md', slug: 'server/guide', title: 'Guide' }, 'Read <a href="../src/Moongate.Api/README.md">the API</a> now.\n', context);
  assert(result.includes('<a href="/libraries/api/">the API</a>'));
});

test('links sample directories to the matching released GitHub tree', async t => {
  const context = await fixture(t);
  assert.equal(resolveDocumentUrl('../src/Moongate.Api/', context), 'https://github.com/moongate-community/moongate/tree/v0.4.0/src/Moongate.Api/');
  assert.throws(() => resolveDocumentUrl('../missing-directory/', context), /cannot resolve/);
});

test('retains the removed Markdown title anchor and explicit HTML title ID', async t => {
  const context = await fixture(t);
  const entry = { source: 'docs/guide.md', slug: 'server/guide', title: 'Guide' };
  const markdown = compileDocument(entry, '# Moongate.Api\n\n[Top](#moongateapi)\n', context);
  assert(markdown.includes('id="moongateapi"'));
  assert(markdown.includes('(#moongateapi)'));
  const html = compileDocument(entry, '<h1 id="custom-title">Old</h1>\n\n[Top](#custom-title)\n', context);
  assert(html.includes('id="custom-title"'));
  assert(!html.includes('<h1'));
});

test('rewrites img and source srcset candidates, preserving descriptors and data URLs', async t => {
  const context = await fixture(t);
  const entry = { source: 'docs/guide.md', slug: 'server/guide', title: 'Guide' };
  const input = '<picture><source srcset="../images/logo.png 640w, ../images/logo.png 1280w"><img src="../images/logo.png" srcset="data:image/png;base64,AAAA 1x, ../images/logo.png 2x"></picture>';
  const html = compileDocument(entry, input, context);
  assert(html.includes('srcset="/generated/images/logo.png 640w, /generated/images/logo.png 1280w"'));
  assert(html.includes('srcset="data:image/png;base64,AAAA 1x, /generated/images/logo.png 2x"'));
});
