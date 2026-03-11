import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [tailwindcss(), react()],
  build: {
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return undefined
          }

          if (id.includes('@xterm/')) {
            return 'vendor-xterm'
          }
          return undefined
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
      '/auth': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
      '/metrics': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
      '/openapi': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
      '/scalar': {
        target: 'http://localhost:8088',
        changeOrigin: true,
      },
    },
  },
})
