import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const apiOrigin = process.env.HUB_API_ORIGIN || 'http://localhost:5080'
const apiProxy = { '/api': apiOrigin, '/health': apiOrigin }

// The API runs on 5080 in development; the SPA calls it through this proxy.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': path.resolve(__dirname, './src') } },
  server: { port: 5173, proxy: apiProxy },
  preview: { proxy: apiProxy },
  build: { outDir: '../src/Hub.Api/wwwroot', emptyOutDir: true, manifest: true },
})
