import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on 5080 in development; the SPA calls it through this proxy.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': path.resolve(__dirname, './src') } },
  server: { port: 5173, proxy: { '/api': 'http://localhost:5080', '/health': 'http://localhost:5080' } },
  build: { outDir: '../src/Hub.Api/wwwroot', emptyOutDir: true },
})
