import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'

const installationPage = await readFile(
  new URL('../docs/.vitepress/dist/server/installation.html', import.meta.url),
  'utf8'
)
const themeStyles = await readFile(
  new URL('../docs/.vitepress/theme/styles/base.css', import.meta.url),
  'utf8'
)

assert.match(
  installationPage,
  /<script id="check-dark-mode">document\.documentElement\.classList\.add\("dark"\);<\/script>/,
  'The generated documentation must force VitePress dark mode before rendering so Shiki uses dark syntax colors.'
)

assert.match(
  themeStyles,
  /\.vp-doc td code\s*\{[^}]*white-space:\s*nowrap;/s,
  'Inline code inside table cells must remain intact instead of being clipped.'
)
