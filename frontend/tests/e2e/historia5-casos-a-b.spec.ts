import { expect, test, type Page } from '@playwright/test'
import { ClienteApi } from './soporte/api.ts'
import { leerEntorno } from './soporte/entorno.ts'

/**
 * Casos A y B de Historia 5 desde la interfaz (decisión D7, RF-053, RF-054, CS-021).
 *
 * Hasta el cierre de Etapa 1, Historia 5 solo era operable por API: los hooks de alta de pertenencia
 * y apertura de contexto existían pero ninguna pantalla los invocaba. Esta prueba verifica que el
 * flujo completo funciona desde la interfaz **y** que produce las mismas relaciones de dominio que
 * la API, contrastándolas con `GET /api/personas/{id}/estado-efectivo`.
 */

async function iniciarSesion(page: Page, correo: string, password: string): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Correo').fill(correo)
  await page.getByLabel('Contraseña', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Ingresar' }).click()
  await expect(page).not.toHaveURL(/\/login$/)
}

function fecha(desplazamientoDias: number): string {
  return new Date(Date.now() + desplazamientoDias * 86_400_000).toISOString().slice(0, 10)
}

/** contracts/people.yaml — EstadoEfectivoPersona (RF-037). */
interface EstadoEfectivo {
  companiaVigenteId: string | null
  contextosOperativosVigentes: {
    contextoOperativoId: string
    companiaPrincipalId: string
  }[]
}

test('Caso A: pertenencia a una Principal fija su contexto automáticamente', async ({ page }) => {
  const entorno = leerEntorno()
  const api = await ClienteApi.iniciarComo('admin')

  try {
    const principalId = await api.crearCompaniaEnAlcance('E2E Caso A', 'PRINCIPAL_MANDANTE')
    const personaId = await api.crearPersona('Caso', 'A')

    await iniciarSesion(page, entorno.admin.correo, entorno.password)
    await page.goto(`/personas/${personaId}/historial`)

    await page.getByRole('button', { name: 'Registrar pertenencia y contexto' }).click()

    const dialogo = page.getByRole('dialog')

    await dialogo.getByLabel('Compañía de pertenencia').selectOption(principalId)

    // RF-053: la Principal del contexto no se elige, la fija la propia pertenencia.
    await expect(dialogo.getByLabel('Compañía principal del contexto')).toBeHidden()
    await expect(dialogo.getByText(/se abre automáticamente con esa misma principal/)).toBeVisible()

    await dialogo.getByLabel('Desde').fill(fecha(-1))
    await dialogo.getByLabel('Hasta').fill(fecha(365))
    await dialogo.getByRole('button', { name: 'Registrar' }).click()

    await expect(page.getByRole('dialog')).toBeHidden()

    // La interfaz produce las mismas relaciones de dominio que la API.
    const estado = await api.exigir<EstadoEfectivo>(
      // `fechaHora` es obligatoria: el estado efectivo siempre se consulta a un instante concreto.
      api.get(
        `/api/personas/${personaId}/estado-efectivo?fechaHora=${encodeURIComponent(new Date().toISOString())}`,
      ),
      200,
    )

    expect(estado.companiaVigenteId).toBe(principalId)
    expect(estado.contextosOperativosVigentes.map((c) => c.companiaPrincipalId)).toContain(principalId)
  } finally {
    await api.cerrar()
  }
})

test('Caso B: personal de contratista elige entre las principales con relación vigente', async ({
  page,
}) => {
  const entorno = leerEntorno()
  const api = await ClienteApi.iniciarComo('admin')

  try {
    const principalId = await api.crearCompaniaEnAlcance('E2E Caso B Principal', 'PRINCIPAL_MANDANTE')
    const contratistaId = await api.crearCompaniaEnAlcance('E2E Caso B Contratista', 'CONTRATISTA')

    await api.exigir(
      api.post(`/api/companias/${contratistaId}/relaciones-principales`, {
        companiaPrincipalId: principalId,
        fechaHoraInicio: new Date(Date.now() - 86_400_000).toISOString(),
      }),
      201,
    )

    const personaId = await api.crearPersona('Caso', 'B')

    await iniciarSesion(page, entorno.admin.correo, entorno.password)
    await page.goto(`/personas/${personaId}/historial`)

    await page.getByRole('button', { name: 'Registrar pertenencia y contexto' }).click()

    const dialogo = page.getByRole('dialog')

    await dialogo.getByLabel('Compañía de pertenencia').selectOption(contratistaId)

    // RF-054: el selector solo ofrece principales con relación vigente con esa contratista.
    const selectorPrincipal = dialogo.getByLabel('Compañía principal del contexto')
    await expect(selectorPrincipal).toBeVisible()
    await selectorPrincipal.selectOption(principalId)

    await dialogo.getByLabel('Desde').fill(fecha(-1))
    await dialogo.getByLabel('Hasta').fill(fecha(365))
    await dialogo.getByRole('button', { name: 'Registrar' }).click()

    await expect(page.getByRole('dialog')).toBeHidden()

    const estado = await api.exigir<EstadoEfectivo>(
      // `fechaHora` es obligatoria: el estado efectivo siempre se consulta a un instante concreto.
      api.get(
        `/api/personas/${personaId}/estado-efectivo?fechaHora=${encodeURIComponent(new Date().toISOString())}`,
      ),
      200,
    )

    // La pertenencia es a la Contratista, pero el contexto operativo es con la Principal.
    expect(estado.companiaVigenteId).toBe(contratistaId)
    expect(estado.contextosOperativosVigentes.map((c) => c.companiaPrincipalId)).toContain(principalId)
  } finally {
    await api.cerrar()
  }
})

test('CS-021: la unidad organizativa se elige sobre el árbol, no sobre una lista plana', async ({
  page,
}) => {
  const entorno = leerEntorno()
  const api = await ClienteApi.iniciarComo('admin')

  try {
    const principalId = await api.crearCompaniaEnAlcance('E2E Árbol', 'PRINCIPAL_MANDANTE')

    const raiz = await api.exigir<{ id: string }>(
      api.post('/api/unidades-organizativas', {
        nombre: `Raíz ${Date.now()}`,
        estado: 'ACTIVO',
        unidadSuperiorId: null,
        companiaPrincipalId: principalId,
      }),
      201,
    )

    await api.exigir(
      api.post('/api/unidades-organizativas', {
        nombre: `Planta Norte ${Date.now()}`,
        estado: 'ACTIVO',
        unidadSuperiorId: raiz.id,
        companiaPrincipalId: null,
      }),
      201,
    )

    const personaId = await api.crearPersona('Caso', 'Árbol')

    const ahora = new Date()
    const inicio = new Date(ahora.getTime() - 86_400_000).toISOString()
    const fin = new Date(ahora.getTime() + 365 * 86_400_000).toISOString()

    await api.exigir(
      api.post(`/api/personas/${personaId}/historial-companias`, {
        companiaId: principalId,
        fechaHoraInicio: inicio,
        fechaHoraFin: fin,
      }),
      201,
    )

    await api.exigir(
      api.post(`/api/personas/${personaId}/contextos-operativos`, {
        companiaPrincipalId: principalId,
        fechaHoraInicio: inicio,
        fechaHoraFin: fin,
      }),
      201,
    )

    await iniciarSesion(page, entorno.admin.correo, entorno.password)
    await page.goto(`/personas/${personaId}/historial`)

    await page.getByRole('button', { name: 'Unidad organizativa' }).first().click()

    // El selector es un árbol accesible (patrón ARIA treeview), no un `<select>` lineal.
    await expect(page.getByRole('tree')).toBeVisible()
    await expect(page.getByRole('treeitem').first()).toBeVisible()
  } finally {
    await api.cerrar()
  }
})
