import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Geliştirme sırasında /api istekleri .NET API'ye yönlendirilir (CORS gerekmeden).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', changeOrigin: true },
      '/connect': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', changeOrigin: true },
      '/.well-known': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', changeOrigin: true },
    },
  },
})
