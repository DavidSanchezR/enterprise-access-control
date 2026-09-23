import react from '@vitejs/plugin-react'
// `defineConfig` viene de vitest/config para conservar el tipado del bloque `test`; `loadEnv` no se
// re-exporta desde ahí y se toma de vite.
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'

export default defineConfig(({ mode }) => {
  /**
   * API a la que el servidor de desarrollo reenvía `/api` y `/health`.
   *
   * Por defecto, la API lanzada con `dotnet run` (perfil http de launchSettings.json). Para usar la
   * API contenedorizada de docker-compose.yml: `VITE_API_PROXY_TARGET=http://localhost:8080`.
   *
   * Se resuelve con `loadEnv` y no solo con `process.env` porque Vite carga los archivos `.env` en
   * `import.meta.env` del cliente, no en el entorno del propio archivo de configuración: leer solo
   * `process.env` hacía que un `VITE_API_PROXY_TARGET` puesto en `frontend/.env` —la forma que
   * documenta `.env.example`— se ignorara en silencio y el proxy siguiera apuntando al destino por
   * defecto, devolviendo `502` en cada llamada a `/api`.
   *
   * El orden de precedencia mantiene `process.env` por delante para no alterar a quien ya exporta la
   * variable en la shell ni a `playwright.config.ts`, que la inyecta por `webServer.env`.
   */
  const env = loadEnv(mode, import.meta.dirname, '')
  const destinoApi =
    process.env.VITE_API_PROXY_TARGET ?? env.VITE_API_PROXY_TARGET ?? 'http://localhost:5290'

  return {
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
  }
})
