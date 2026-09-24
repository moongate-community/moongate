// Authoritative source paths, public routes, and navigation placement.
// `group` is the sidebar section, ordered by the reader's role; `subgroup` is an
// optional collapsible section inside it. Entries keep their manifest order.
export const contentEntries = [
  // Start here: install, prepare, first start, what exists today.
  { source: 'README.md', slug: 'start/overview', title: 'Overview', group: 'Start here' },
  { source: 'docs/installation.md', slug: 'start/install', title: 'Install on Linux', group: 'Start here' },
  { source: 'docs/mgboot.md', slug: 'start/mgboot', title: 'Prepare a root with mgboot', group: 'Start here' },
  { source: 'docs/getting-started.md', slug: 'start/getting-started', title: 'First start', group: 'Start here' },
  { source: 'docs/implementation-status.md', slug: 'start/implementation-status', title: 'Implementation status', group: 'Start here' },
  { source: 'CHANGELOG.md', slug: 'start/changelog', title: 'Changelog', group: 'Start here' },

  // Run a shard: the operator's pages.
  { source: 'docs/docker.md', slug: 'server/docker', title: 'Run with Docker', group: 'Run a shard' },
  { source: 'docs/docker-login-realms.md', slug: 'server/docker-login-realms', title: 'Docker login and realms', group: 'Run a shard' },
  { source: 'docs/server-configuration.md', slug: 'server/configuration', title: 'Configuration', group: 'Run a shard' },
  { source: 'docs/persistence-operations.md', slug: 'server/persistence-operations', title: 'Operate PostgreSQL', group: 'Run a shard' },
  { source: 'docs/diagnostics.md', slug: 'server/diagnostics', title: 'Diagnostics', group: 'Run a shard' },

  // Scripting and content: shard content without C#.
  { source: 'docs/scripting.md', slug: 'server/scripting', title: 'Writing Lua scripts', group: 'Scripting and content' },
  { source: 'docs/templates.md', slug: 'server/templates', title: 'Loading TOML templates', group: 'Scripting and content' },
  { source: 'docs/uox3-migration.md', slug: 'server/uox3-migration', title: 'Migrate from UOX3', group: 'Scripting and content' },

  // Extend with C#: plugins and the subsystems they plug into.
  { source: 'docs/plugins.md', slug: 'server/plugins', title: 'Writing a plugin', group: 'Extend with C#', subgroup: 'Plugins' },
  { source: 'docs/lua-modules.md', slug: 'server/lua-modules', title: 'Writing a Lua module', group: 'Extend with C#', subgroup: 'Plugins' },
  { source: 'docs/metric-providers.md', slug: 'server/metric-providers', title: 'Registering a metric provider', group: 'Extend with C#', subgroup: 'Plugins' },
  { source: 'docs/persistence.md', slug: 'server/persistence', title: 'Entities and data access', group: 'Extend with C#', subgroup: 'Persistence' },
  { source: 'docs/persistence-entity-tutorial.md', slug: 'server/persistence-entity-tutorial', title: 'Create a persistent entity', group: 'Extend with C#', subgroup: 'Persistence' },
  { source: 'docs/persistence-migrations.md', slug: 'server/persistence-migrations', title: 'Migrations: generate, review and apply', group: 'Extend with C#', subgroup: 'Persistence' },
  { source: 'docs/packets.md', slug: 'server/packets', title: 'Packets and handlers', group: 'Extend with C#', subgroup: 'Network and game loop' },
  { source: 'docs/network-game-separation.md', slug: 'server/network-game-separation', title: 'Transport and game ownership', group: 'Extend with C#', subgroup: 'Network and game loop' },
  { source: 'docs/game-loop-and-timers.md', slug: 'server/game-loop-and-timers', title: 'Game loop and timers', group: 'Extend with C#', subgroup: 'Network and game loop' },

  // Libraries: the NuGet packages, usable without the server.
  { source: 'docs/nuget-packaging.md', slug: 'reference/nuget-packaging', title: 'NuGet packages', group: 'Libraries' },
  { source: 'docs/network.md', slug: 'libraries/network-cookbook', title: 'Standalone TCP cookbook', group: 'Libraries' },
  ...[
    ['Core', 'core'], ['Network', 'network'], ['Network.Packets', 'network-packets'],
    ['Persistence', 'persistence'], ['Persistence.Migrations', 'persistence-migrations'],
    ['Server.Core', 'server-core'], ['Scripting', 'scripting'], ['Ultima', 'ultima'],
  ].map(([name, slug]) => ({
    source: `src/Moongate.${name}/README.md`, slug: `libraries/${slug}`,
    title: `Moongate.${name}`, group: 'Libraries',
  })),

  // Contributing.
  { source: 'CONTRIBUTING.md', slug: 'contributing/getting-started', title: 'Contribute to Moongate', group: 'Contributing' },
  { source: 'docs/documentation.md', slug: 'contributing/documentation', title: 'Writing documentation', group: 'Contributing' },
  { source: 'docs/persistence-stress.md', slug: 'contributing/persistence-stress', title: 'Stress-test persistence', group: 'Contributing' },
  { source: 'docs/security-audit.md', slug: 'contributing/security-audit', title: 'Dependency security', group: 'Contributing' },
];

export const sidebarGroups = ['Start here', 'Run a shard', 'Scripting and content', 'Extend with C#', 'Libraries', 'Contributing'];
