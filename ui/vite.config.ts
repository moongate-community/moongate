// From 'vitest/config', not 'vite': it is the same defineConfig widened to accept the `test` key below.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath } from 'node:url'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  // `@/` is what the shadcn registry writes into every component it generates, and it has to resolve in
  // three places: here for the bundle and for Vitest, and in tsconfig paths for the typecheck. Without it
  // the CLI cannot expand the alias either, and writes its output into a directory literally named `@`.
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  // Vite's own dist/. The server finds it rather than the build pushing into a C# project: it looks at
  // the configured path, then $MOONGATE_UI_DIST, then ./ui/dist, then ui/dist beside the executable.
  build: { outDir: 'dist', emptyOutDir: true },
  server: { proxy: { '/api': 'http://127.0.0.1:8933' } },
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    globals: true,
  },
})
