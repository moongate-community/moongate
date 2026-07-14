import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'

const installationPage = await readFile(
  new URL('../docs/.vitepress/dist/server/installation.html', import.meta.url),
  'utf8'
)

assert.match(
  installationPage,
  /<script id="check-dark-mode">document\.documentElement\.classList\.add\("dark"\);<\/script>/,
  'The generated documentation must force VitePress dark mode before rendering so Shiki uses dark syntax colors.'
)
