import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

/**
 * API a la que el servidor de desarrollo reenvía `/api` y `/health`.
 *
 * Por defecto, la API lanzada con `dotnet run` (perfil http de launchSettings.json). Para usar la API
 * contenedorizada de docker-compose.yml: `VITE_API_PROXY_TARGET=http://localhost:8080`.
 */
const destinoApi = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5290'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Mismo origen para la SPA y la API: el navegador no necesita CORS (ver src/lib/apiClient.ts).
    proxy: {
      '/api': { target: destinoApi, changeOrigin: true },
      '/health': { target: destinoApi, changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./tests/unit/setup.ts'],
    include: ['tests/unit/**/*.{test,spec}.{ts,tsx}'],
  },
})
