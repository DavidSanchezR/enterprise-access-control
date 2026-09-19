import { defineConfig, devices } from '@playwright/test'

/** API real contra la que corren los escenarios de quickstart.md (docker-compose.yml, puerto 8080). */
const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:8080'

export default defineConfig({
  testDir: './tests/e2e',
  // Prepara SQL Server, la API contenedorizada, las migraciones y los usuarios de prueba.
  globalSetup: './tests/e2e/soporte/preparacion-global.ts',
  // En serie: los escenarios amplían el alcance del mismo usuario con PUT (reemplazo completo), y dos
  // escrituras concurrentes perderían una de las compañías.
  fullyParallel: false,
  workers: 1,
  timeout: 120_000,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: !process.env.CI,
    // El proxy de mismo origen de Vite apunta a la API de las pruebas (quickstart.md §3).
    env: { VITE_API_PROXY_TARGET: apiUrl },
  },
})
