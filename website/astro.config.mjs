import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import { contentEntries } from './content-manifest.mjs';

const groups = ['Start here', 'Server guides', 'Libraries', 'Reference', 'Contributing'];
const version = process.env.MOONGATE_DOCS_VERSION || 'development';
export default defineConfig({
  site: 'https://moongate-community.github.io',
  base: '/moongate',
  trailingSlash: 'always',
  integrations: [starlight({
    title: `Moongate ${version}`,
    routeMiddleware: './src/route-data.ts',
    favicon: '/generated/images/moongate_logo.png',
    description: 'An Ultima Online server and reusable .NET libraries.',
    logo: { src: '../images/moongate_logo.png', alt: 'Moongate' },
    social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/moongate-community/moongate' }],
    sidebar: groups.map(label => ({
      label,
      items: contentEntries.filter(entry => entry.group === label).map(entry => ({ slug: entry.slug })),
    })),
  })],
});
