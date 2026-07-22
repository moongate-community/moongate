// Regenerates ui/openapi.json from the running server.
//
// The document is read from a live server rather than produced offline: HttpServerService builds its
// WebApplication inside a hosted service rather than from a conventional entry point, so Swashbuckle's
// CLI has no host to find. Booting costs a few seconds, which is cheap for a step that runs on demand
// and once in CI.
import { spawn } from 'node:child_process'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Resolved from this file rather than the working directory, so the script behaves the same whether npm
// runs it from ui/ or someone calls it by path from the repository root.
const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = resolve(uiRoot, '..')

// Deliberately not the defaults (8933 and 2593). A developer with a server already running would
// otherwise have this script read *that* server's document while the one it spawned dies unable to bind
// — a contract check that silently inspects the wrong process.
const HTTP_PORT = 18933
const GAME_PORT = 12593
const DOCUMENT_URL = `http://127.0.0.1:${HTTP_PORT}/swagger/v1/swagger.json`
const TIMEOUT_MS = 120_000
const SHUTDOWN_MS = 15_000

const root = await mkdtemp(join(tmpdir(), 'moongate-openapi-'))

// A throwaway root gets a throwaway config. Only these two sections are written; the server seeds the
// rest of moongate.yaml around them on first boot. Both bind to the loopback, so nothing is exposed
// while it runs.
await writeFile(
  join(root, 'moongate.yaml'),
  [
    'http:',
    '  Address: 127.0.0.1',
    `  Port: ${HTTP_PORT}`,
    'moongate:',
    '  Network:',
    '    Address: 127.0.0.1',
    `    Port: ${GAME_PORT}`,
    '',
  ].join('\n'),
)

// The UO directory is the throwaway root too: the API surface does not depend on the client files, and
// the server boots without them — which is what lets this run on a CI machine that has none.
const server = spawn(
  'dotnet',
  ['run', '--project', 'src/Moongate.Server', '--', '--root-directory', root, '--uo-directory', root],
  { cwd: repoRoot, stdio: ['ignore', 'pipe', 'pipe'] },
)

let log = ''
server.stdout.on('data', (chunk) => (log += chunk))
server.stderr.on('data', (chunk) => (log += chunk))

const exited = new Promise((onExit) => server.once('exit', onExit))

const started = Date.now()
let document = null

while (Date.now() - started < TIMEOUT_MS) {
  if (server.exitCode !== null) {
    break
  }

  try {
    const response = await fetch(DOCUMENT_URL)
    if (response.ok) {
      document = await response.json()
      break
    }
  } catch {
    // Not listening yet.
  }

  await new Promise((tick) => setTimeout(tick, 500))
}

server.kill('SIGTERM')

// A server that will not stop must not hold the whole job: SIGKILL after a grace period, so the worst
// case is a lost temporary directory rather than a CI run that hangs until its timeout.
const stopped = await Promise.race([
  exited.then(() => true),
  new Promise((onTimeout) => setTimeout(() => onTimeout(false), SHUTDOWN_MS)),
])

if (!stopped) {
  server.kill('SIGKILL')
  await exited
}

await rm(root, { recursive: true, force: true })

if (document === null) {
  console.error(log)
  throw new Error(`Could not read ${DOCUMENT_URL} within ${TIMEOUT_MS}ms`)
}

await writeFile(join(uiRoot, 'openapi.json'), `${JSON.stringify(document, null, 2)}\n`)
console.log(`wrote openapi.json (${Object.keys(document.paths).length} paths)`)
