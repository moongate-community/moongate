// Authoritative source paths, public routes, and navigation placement.
export const contentEntries = [
  { source: 'README.md', slug: 'start/overview', title: 'Overview', group: 'Start here' },
  { source: 'docs/diagnostics.md', slug: 'server/diagnostics', title: 'Diagnostics', group: 'Server guides' },
  { source: 'docs/network-game-separation.md', slug: 'server/network-game-separation', title: 'Transport and game ownership', group: 'Server guides' },
  { source: 'docs/persistence-format.md', slug: 'reference/persistence-format', title: 'Persistence format', group: 'Reference' },
  { source: 'docs/nuget-packaging.md', slug: 'reference/nuget-packaging', title: 'NuGet packages', group: 'Reference' },
  { source: 'docs/security-audit.md', slug: 'contributing/security-audit', title: 'Dependency security', group: 'Contributing' },
  { source: 'docs/documentation.md', slug: 'contributing/documentation', title: 'Writing documentation', group: 'Contributing' },
  ...[
    ['Core', 'core'], ['Network', 'network'], ['Network.Packets', 'network-packets'],
    ['Persistence', 'persistence'], ['Server.Core', 'server-core'], ['Api', 'api'],
    ['Scripting', 'scripting'], ['Ultima', 'ultima'],
  ].map(([name, slug]) => ({
    source: `src/Moongate.${name}/README.md`, slug: `libraries/${slug}`,
    title: `Moongate.${name}`, group: 'Libraries',
  })),
];
