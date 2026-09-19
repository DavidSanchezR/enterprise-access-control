import { expect, test } from '@playwright/test'
import { leerEntorno } from './soporte/entorno.ts'

/**
 * La SPA llega a la API real por el proxy de mismo origen de Vite (quickstart.md §3). Las pruebas de §5 y
 * §6 hablan directamente con la API; esta confirma que la interfaz está cableada a ella.
 */
test('la SPA inicia sesión contra la API real y carga datos protegidos', async ({ page }) => {
  const entorno = leerEntorno()

  await page.goto('/companias')
  await expect(page).toHaveURL(/\/login$/)

  await page.getByLabel('Correo').fill(entorno.admin.correo)
  await page.getByLabel('Contraseña', { exact: true }).fill(entorno.password)

  const companias = page.waitForResponse(
    (r) => new URL(r.url()).pathname === '/api/companias' && r.request().method() === 'GET',
  )

  await page.getByRole('button', { name: 'Ingresar' }).click()

  expect((await companias).status()).toBe(200)
  await expect(page.getByRole('heading', { level: 1, name: 'Compañías' })).toBeVisible()
})
