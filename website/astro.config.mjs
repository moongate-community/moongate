import { readFileSync } from 'node:fs';
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { contentEntries, sidebarGroups } from './content-manifest.mjs';
import { docsSite, docsBasePath } from './site-config.mjs';

const releaseVersion = JSON.parse(readFileSync(new URL('../.release-please-manifest.json', import.meta.url), 'utf8'))['.'];
const version = process.env.MOONGATE_DOCS_VERSION || `v${releaseVersion}`;
// Pages without a subgroup become links; consecutive pages sharing a subgroup
// become one collapsible section, in manifest order.
function sidebarItems(entries) {
  const items = [];
  for (const entry of entries) {
    if (!entry.subgroup) {
      items.push({ slug: entry.slug });
      continue;
    }
    const last = items.at(-1);
    if (last?.label === entry.subgroup) {
      last.items.push({ slug: entry.slug });
    } else {
      items.push({ label: entry.subgroup, collapsed: true, items: [{ slug: entry.slug }] });
    }
  }
  return items;
}

export default defineConfig({
  site: docsSite,
  base: docsBasePath,
  trailingSlash: 'always',
  integrations: [starlight({
    title: `Moongate ${version}`,
    routeMiddleware: './src/route-data.ts',
    favicon: '/favicon.png',
    description: 'An Ultima Online server and reusable .NET libraries.',
    logo: { src: '../images/moongate_mark.png', alt: 'Moongate' },
    customCss: ['./src/styles/custom.css'],
    social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/moongate-community/moongate' }],
    sidebar: sidebarGroups.map(label => ({ label, items: sidebarItems(contentEntries.filter(entry => entry.group === label)) })),
  })],
});
