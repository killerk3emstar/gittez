import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwind from '@tailwindcss/vite'

// Proxy trzyma front i API na jednym originie, więc backend nie potrzebuje
// CORS-u, a nagłówek X-Data-Stale jest widoczny dla fetcha bez expose-headers.
export default defineConfig({
  plugins: [react(), tailwind()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8081',
        changeOrigin: true,
      },
    },
  },
})
