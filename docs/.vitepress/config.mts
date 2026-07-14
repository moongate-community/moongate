import { defineConfig } from 'vitepress'

export default defineConfig({
  lang: 'en-US',
  title: 'Moongate',
  description: 'Operator and contributor documentation for the Moongate Ultima Online server emulator.',
  cleanUrls: true,
  lastUpdated: true,
  appearance: false,
  ignoreDeadLinks: false,
  themeConfig: {
    logo: '/images/moongate-logo.png',
    siteTitle: 'Moongate',
    search: { provider: 'local' },
    nav: [
      { text: 'Server Guide', link: '/server/' },
      { text: 'Contributors', link: '/contributors/' },
      { text: 'Architecture', link: '/architecture/' }
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/moongate-community/moongate' }
    ],
    sidebar: {
      '/server/': [
        {
          text: 'Server Guide',
          items: [
            { text: 'Overview', link: '/server/' },
            { text: 'Installation', link: '/server/installation' },
            { text: 'Configuration', link: '/server/configuration' },
            { text: 'First launch', link: '/server/first-launch' },
            { text: 'Operations', link: '/server/operations' },
            { text: 'Troubleshooting', link: '/server/troubleshooting' }
          ]
        }
      ],
      '/contributors/': [{ text: 'Contributors', items: [{ text: 'Overview', link: '/contributors/' }] }],
      '/architecture/': [{ text: 'Architecture', items: [{ text: 'Overview', link: '/architecture/' }] }]
    },
    outline: { level: [2, 3], label: 'On this page' },
    docFooter: { prev: 'Previous page', next: 'Next page' },
    lastUpdated: { text: 'Last updated' }
  }
})
