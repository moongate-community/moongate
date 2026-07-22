// Regenerates ui/src/lib/api-types.ts from ui/openapi.json.
//
// The generator runs through npx in a tree of its own rather than as a devDependency, because it needs
// something this project does not have. openapi-typescript builds its output through the TypeScript
// compiler API, and typescript@7 is the native port: its lib/ ships tsc.js and nothing else, so
// `ts.factory` is undefined and the generator dies on import. Its peer range (^5.x) says as much.
//
// Both versions are pinned exactly, so the document always maps to the same types — a contract check
// that drifted with a floating generator would report changes nobody made.
import { spawnSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')

const TYPESCRIPT = 'typescript@5.9.3'
const GENERATOR = 'openapi-typescript@7.13.0'

const result = spawnSync(
  'npx',
  [
    '--yes',
    '-p',
    TYPESCRIPT,
    '-p',
    GENERATOR,
    'openapi-typescript',
    'openapi.json',
    '-o',
    'src/lib/api-types.ts',
  ],
  { cwd: uiRoot, stdio: 'inherit' },
)

if (result.error) {
  throw result.error
}

process.exit(result.status ?? 1)
