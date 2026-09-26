import { expect, test, type Page } from '@playwright/test'
import { leerEntorno } from './soporte/entorno.ts'

/**
 * UX-17 a UX-22 desde la interfaz (ux-ui.md §35, RF-074 a RF-077).
 *
 * Ejercita el módulo completo con el usuario `admin`, que es GLOBAL_ADMINISTRATOR: crear un usuario
 * con su primera asignación, consultar sus asignaciones, agregar otra y finalizarla. El aislamiento
 * del COMPANY_ADMINISTRATOR se verifica con `ajeno`, cuyo alcance es otra compañía.
 */

async function iniciarSesion(page: Page, correo: string, password: string): Promise<void> {
  await page.goto('/login')
  await page.getByLabel('Correo').fill(correo)
  await page.getByLabel('Contraseña', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Ingresar' }).click()
  await expect(page).not.toHaveURL(/\/login$/)
}

/** `datetime-local` espera `YYYY-MM-DDTHH:mm` en hora local del navegador. */
function valorLocal(desplazamientoDias: number): string {
  const fecha = new Date(Date.now() + desplazamientoDias * 86_400_000)
  const desfase = fecha.getTimezoneOffset() * 60_000
  return new Date(fecha.getTime() - desfase).toISOString().slice(0, 16)
}

/**
 * Localiza la fila de un usuario buscándolo por correo.
 *
 * Desde el cierre de la desviación D-4, `GET /api/usuarios` acepta `texto` y la búsqueda se resuelve
 * en el servidor sobre todo el conjunto dentro del alcance, antes de paginar (RF-077, UX-22): ya no
 * hace falta recorrer páginas para encontrar un alta reciente en una base que acumula usuarios entre
 * ejecuciones. Si la búsqueda volviera a filtrar solo la página cargada, este ayudante fallaría y con
 * él todas las pruebas que lo usan.
 */
async function filaDelUsuario(page: Page, correo: string) {
  // Se ancla al input del filtro por su id: `getByLabel('Correo')` también alcanzaría al campo del
  // wizard de alta mientras su diálogo se cierra, y se rellenaría el formulario en vez del filtro.
  await page.locator('#filtro-texto').fill(correo)

  const fila = page.getByRole('row', { name: new RegExp(correo) })
  await fila.first().waitFor({ state: 'visible', timeout: 10_000 })

  return fila
}

test('UX-17 a UX-22: alta de usuario con rol, detalle, asignación y finalización', async ({
  page,
}) => {
  const entorno = leerEntorno()
  const correoNuevo = `e2e.ux.${Date.now()}@empresa.cl`

  await iniciarSesion(page, entorno.admin.correo, entorno.password)

  await page.goto('/usuarios')
  await expect(
    page.getByRole('heading', { level: 1, name: 'Usuarios y roles administrativos' }),
  ).toBeVisible()

  await test.step('UX-17 — crear usuario con su primera asignación de rol', async () => {
    await page.getByRole('button', { name: 'Nuevo usuario' }).click()

    const dialogo = page.getByRole('dialog')

    // Paso 1 — identidad.
    await dialogo.getByLabel('Correo').fill(correoNuevo)
    await dialogo.getByLabel('Contraseña inicial').fill(entorno.password)
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    // Paso 2 — rol: un GLOBAL_ADMINISTRATOR ve los dos roles del catálogo cerrado (RF-074).
    await expect(dialogo.getByLabel('Administrador global')).toBeVisible()

    // VF-010: cada radio conserva su ancho nativo y los dos quedan alineados en la misma columna. La
    // regla global `input { width: 100% }` los estiraba (~330 px) y los desplazaba. jsdom no calcula
    // maquetación, así que esta es la única prueba que puede detectarlo.
    const cajas = await Promise.all(
      ['Administrador global', 'Administrador de compañía'].map((rol) =>
        dialogo.getByLabel(rol).boundingBox(),
      ),
    )
    for (const caja of cajas) {
      expect(caja!.width).toBeLessThanOrEqual(24)
    }
    expect(Math.abs(cajas[0]!.x - cajas[1]!.x)).toBeLessThanOrEqual(1)

    await dialogo.getByLabel('Administrador de compañía').check()
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    // Paso 3 — compañía, obligatoria para COMPANY_ADMINISTRATOR.
    // `exact` evita colisionar con el radio "Administrador de compañía" del paso anterior.
    await dialogo.getByLabel('Compañía', { exact: true }).selectOption({ index: 1 })
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    // Paso 4 — vigencia obligatoria por ambos extremos (RF-075).
    await dialogo.getByLabel('Inicio de vigencia').fill(valorLocal(-1))
    await dialogo.getByLabel('Fin de vigencia').fill(valorLocal(365))
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    // Paso 5 — confirmación.
    await dialogo.getByRole('button', { name: 'Crear usuario' }).click()

    // El wizard es modal: hasta que se cierre, el filtro del listado que hay detrás no es alcanzable.
    await expect(dialogo).toBeHidden()

    await expect(await filaDelUsuario(page, correoNuevo)).toBeVisible()
  })

  await test.step('UX-22 — el listado muestra rol y compañía como columnas distintas', async () => {
    const fila = await filaDelUsuario(page, correoNuevo)

    await expect(fila.getByText('Administrador de compañía')).toBeVisible()
    await expect(fila).toContainText('debe cambiar su contraseña')
  })

  await test.step('UX-18 — el detalle lista las asignaciones con su vigencia', async () => {
    const fila = await filaDelUsuario(page, correoNuevo)
    await fila.getByRole('button', { name: 'Ver' }).click()

    const detalle = page.getByRole('dialog')
    await detalle.getByRole('tab', { name: 'Asignaciones' }).click()

    await expect(detalle.getByRole('cell', { name: 'Administrador de compañía' })).toBeVisible()
    await expect(detalle.getByRole('cell', { name: 'Sí', exact: true })).toBeVisible()

    await detalle.getByRole('button', { name: 'Cerrar', exact: true }).click()
  })

  await test.step('UX-19 — asignar otro rol agrega, no reemplaza', async () => {
    const fila = await filaDelUsuario(page, correoNuevo)
    await fila.getByRole('button', { name: 'Asignar rol' }).click()

    const dialogo = page.getByRole('dialog')

    await expect(dialogo.getByText(/Esta operación/)).toContainText('agrega')

    await dialogo.getByLabel('Administrador global').check()
    await dialogo.getByLabel('Inicio de vigencia').fill(valorLocal(-1))
    await dialogo.getByLabel('Fin de vigencia').fill(valorLocal(200))

    await dialogo.getByRole('button', { name: 'Agregar asignación' }).click()

    await expect(page.getByRole('dialog')).toBeHidden()
  })

  await test.step('UX-20 — renovar una asignación extiende su vigencia con confirmación', async () => {
    const fila = await filaDelUsuario(page, correoNuevo)
    await fila.getByRole('button', { name: 'Ver' }).click()

    const detalle = page.getByRole('dialog')
    await detalle.getByRole('tab', { name: 'Asignaciones' }).click()

    const filas = detalle.locator('tbody tr')
    const cuantasAntes = await filas.count()

    await detalle.getByRole('button', { name: 'Renovar' }).first().click()

    const confirmacion = page.getByRole('alertdialog')
    await expect(confirmacion).toBeVisible()

    // Más lejos que cualquier fin sembrado por la prueba: la renovación exige fecha posterior.
    await confirmacion.getByLabel('Nuevo fin de vigencia').fill(valorLocal(900))
    await confirmacion.getByRole('button', { name: 'Renovar asignación' }).click()

    await expect(confirmacion).toBeHidden()

    // Renovar NO crea una asignación nueva: extiende la existente (RF-075).
    await expect(filas).toHaveCount(cuantasAntes)

    await detalle.getByRole('button', { name: 'Cerrar', exact: true }).click()
  })

  await test.step('UX-20 — finalizar una asignación exige confirmación', async () => {
    const fila = await filaDelUsuario(page, correoNuevo)
    await fila.getByRole('button', { name: 'Ver' }).click()

    const detalle = page.getByRole('dialog')
    await detalle.getByRole('tab', { name: 'Asignaciones' }).click()

    await detalle.getByRole('button', { name: 'Finalizar' }).first().click()

    const confirmacion = page.getByRole('alertdialog')
    await expect(confirmacion).toBeVisible()
    await confirmacion.getByRole('button', { name: 'Finalizar asignación' }).click()

    // El registro se conserva: sigue listado, pero ya no vigente (Principio IV).
    await expect(detalle.getByRole('cell', { name: 'No', exact: true })).toBeVisible()
  })
})

test('UX-22: la búsqueda encuentra a un usuario que no está en la primera página', async ({
  page,
}) => {
  const entorno = leerEntorno()

  await iniciarSesion(page, entorno.admin.correo, entorno.password)
  await page.goto('/usuarios')

  // Correo deliberadamente al final del orden alfabético para que caiga en la última página: el
  // listado ordena por correo ascendente y la base acumula usuarios entre ejecuciones.
  const correoBuscado = `zzz.e2e.busqueda.${Date.now()}@empresa.cl`

  await test.step('alta del usuario objetivo', async () => {
    await page.getByRole('button', { name: 'Nuevo usuario' }).click()

    const dialogo = page.getByRole('dialog')

    await dialogo.getByLabel('Correo').fill(correoBuscado)
    await dialogo.getByLabel('Contraseña inicial').fill(entorno.password)
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    await dialogo.getByLabel('Administrador de compañía').check()
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    await dialogo.getByLabel('Compañía', { exact: true }).selectOption({ index: 1 })
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    await dialogo.getByLabel('Inicio de vigencia').fill(valorLocal(-1))
    await dialogo.getByLabel('Fin de vigencia').fill(valorLocal(365))
    await dialogo.getByRole('button', { name: 'Siguiente' }).click()

    await dialogo.getByRole('button', { name: 'Crear usuario' }).click()
    await expect(dialogo).toBeHidden()
  })

  await test.step('sin buscar, el usuario no está en la página visible', async () => {
    // Se comprueba la premisa en lugar de asumirla: si estuviera en la página 1, la prueba no
    // demostraría nada sobre la búsqueda server-side.
    await expect(page.getByRole('row', { name: new RegExp(correoBuscado) })).toHaveCount(0)
  })

  await test.step('la búsqueda lo encuentra sin recorrer la paginación', async () => {
    await page.locator('#filtro-texto').fill(correoBuscado)

    await expect(page.getByRole('row', { name: new RegExp(correoBuscado) })).toBeVisible()

    // El total pasa a contar las coincidencias del alcance, no el universo del sistema.
    await expect(page.getByText(/1 usuarios en su alcance/)).toBeVisible()
  })

  await test.step('un fragmento inexistente devuelve vacío, no un error', async () => {
    await page.locator('#filtro-texto').fill(`no-existe-${Date.now()}`)

    await expect(page.getByText('No hay usuarios que coincidan con los filtros aplicados.')).toBeVisible()
  })
})

test('UX-21: un COMPANY_ADMINISTRATOR no ve usuarios fuera de su compañía', async ({ page }) => {
  const entorno = leerEntorno()

  await iniciarSesion(page, entorno.ajeno.correo, entorno.password)

  await page.goto('/usuarios')

  // RF-077: el listado no revela la existencia de usuarios fuera del alcance, ni en los totales.
  await expect(page.getByRole('cell', { name: entorno.admin.correo })).toBeHidden()

  await expect(
    page.getByText('Su rol administra únicamente usuarios de su compañía'),
  ).toBeVisible()

  // El rol global ni siquiera se ofrece en el alta (RF-076, ux-ui.md §35 paso 2).
  await page.getByRole('button', { name: 'Nuevo usuario' }).click()

  const dialogo = page.getByRole('dialog')
  await dialogo.getByLabel('Correo').fill(`e2e.rechazo.${Date.now()}@empresa.cl`)
  await dialogo.getByLabel('Contraseña inicial').fill(entorno.password)
  await dialogo.getByRole('button', { name: 'Siguiente' }).click()

  await expect(dialogo.getByLabel('Administrador global')).toBeHidden()
  await expect(dialogo.getByLabel('Administrador de compañía')).toBeVisible()
})
