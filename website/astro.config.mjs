import { readFileSync } from 'node:fs';
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { contentEntries } from './content-manifest.mjs';
import { docsSite, docsBasePath } from './site-config.mjs';

const groups = ['Start here', 'Server guides', 'Libraries', 'Reference', 'Contributing'];
const releaseVersion = JSON.parse(readFileSync(new URL('../.release-please-manifest.json', import.meta.url), 'utf8'))['.'];
const version = process.env.MOONGATE_DOCS_VERSION || `v${releaseVersion}`;
export default defineConfig({
  site: docsSite,
  base: docsBasePath,
  trailingSlash: 'always',
  integrations: [starlight({
    title: `Moongate ${version}`,
    routeMiddleware: './src/route-data.ts',
    favicon: '/generated/images/moongate_logo.png',
    description: 'An Ultima Online server and reusable .NET libraries.',
    logo: { src: '../images/moongate_logo.png', alt: 'Moongate' },
    customCss: ['./src/styles/custom.css'],
    social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/moongate-community/moongate' }],
    sidebar: groups.map(label => ({
      label,
      items: contentEntries.filter(entry => entry.group === label).map(entry => ({ slug: entry.slug })),
    })),
  })],
});
