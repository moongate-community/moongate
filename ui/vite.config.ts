// From 'vitest/config', not 'vite': it is the same defineConfig widened to accept the `test` key below.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
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
