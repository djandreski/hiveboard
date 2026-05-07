import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// In `dev`, /api/v1 requests are proxied to the local API host so the
// dashboard can run standalone without CORS configuration. The proxy
// target is configurable via VITE_API_PROXY_TARGET.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000'

  return {
    plugins: [react()],
    server: {
      host: true,
      port: 5173,
      proxy: {
        '/api': {
          target: proxyTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    preview: {
      host: true,
      port: 4173,
    },
  }
})
