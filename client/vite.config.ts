import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { fileURLToPath } from 'node:url'
import { resolve } from 'node:path'

const root = fileURLToPath(new URL('.', import.meta.url))

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), { name: 'reader-short-path', configureServer(server) { server.middlewares.use((request, _response, next) => { if (request.url === '/read' || request.url?.startsWith('/read?')) request.url = '/read.html' + request.url.slice(5); next() }) } }],
  build: { rollupOptions: { input: { landing: resolve(root, 'index.html'), app: resolve(root, 'app.html'), sample: resolve(root, 'sample.html'), read: resolve(root, 'read.html') } } },
  server: { proxy: { '/api': 'http://localhost:5080' } },
})
