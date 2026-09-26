import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania } from '../../src/features/companies/api'
import * as api from '../../src/features/users/api'
import type { AsignacionRol, Usuario } from '../../src/features/users/api'
import { UsuarioDetalle } from '../../src/features/users/UsuarioDetalle'
import { ApiError } from '../../src/lib/apiClient'

/**
 * Renovación de una asignación de rol desde el detalle (UX-20; cierre de la desviación D-1).
 *
 * Hasta la Sesión 2026-09-20 la interfaz solo ofrecía finalizar: la renovación existía en el dominio
 * pero no tenía endpoint, así que tampoco botón. Estas pruebas fijan que exista el flujo completo y
 * que ningún control quede sin llamada real detrás.
 */

const USUARIO_ID = '0199b0d0-0000-7000-8000-000000000001'
const ASIGNACION_ID = '0199b0d0-0000-7000-8000-00000000aa01'
const COMPANIA_ID = '0199b0d0-0000-7000-8000-0000000000aa'

function compania(): Compania {
  return {
    id: COMPANIA_ID,
    nombre: 'Minera Propia',
    tipoDocumentoId: '0199b0d0-0001-7000-8000-000000000001',
    numeroDocumento: '20100000001',
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    zonaHorariaIana: 'America/Lima',
  }
}

/** Página de compañías como la devuelve `listarCompanias` (la usa el detalle desde VF-011). */
function companias(items: Compania[]) {
  return { items, total: items.length, pagina: 1, tamañoPagina: 200 }
}

function asignacion(overrides: Partial<AsignacionRol> = {}): AsignacionRol {
  return {
    id: ASIGNACION_ID,
    usuarioId: USUARIO_ID,
    rol: 'COMPANY_ADMINISTRATOR',
    companiaId: COMPANIA_ID,
    fechaHoraInicio: '2026-01-01T00:00:00Z',
    fechaHoraFin: '2027-01-01T00:00:00Z',
    vigente: true,
    ...overrides,
  }
}

function usuario(): Usuario {
  return {
    id: USUARIO_ID,
    correo: 'admin@empresa.cl',
    estado: 'ACTIVO',
    requiereCambioPassword: false,
    asignacionesRol: [asignacion()],
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <UsuarioDetalle usuario={usuario()} alCerrar={() => {}} />
    </QueryClientProvider>,
  )
}

/** Abre la pestaña de asignaciones y devuelve la fila de la asignación vigente. */
async function abrirAsignaciones(): Promise<HTMLElement> {
  renderizar()

  await userEvent.click(screen.getByRole('tab', { name: 'Asignaciones' }))

  return (await screen.findByText('Administrador de compañía')).closest('tr')!
}

describe('UsuarioDetalle — renovación (UX-20)', () => {
  beforeEach(() => {
    vi.spyOn(api, 'listarRoles').mockResolvedValue([asignacion()])
    // Desde VF-011 el detalle resuelve nombres de compañía: sin este mock habría una llamada real.
    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(companias([compania()]))
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('ofrece renovar y finalizar sobre una asignación vigente', async () => {
    const fila = await abrirAsignaciones()

    expect(within(fila).getByRole('button', { name: 'Renovar' })).toBeInTheDocument()
    expect(within(fila).getByRole('button', { name: 'Finalizar' })).toBeInTheDocument()
  })

  it('no ofrece ninguna de las dos acciones sobre una asignación ya no vigente', async () => {
    // Una asignación expirada no se renueva ni se finaliza: exige una nueva (RF-075).
    vi.spyOn(api, 'listarRoles').mockResolvedValue([asignacion({ vigente: false })])

    const fila = await abrirAsignaciones()

    expect(within(fila).queryByRole('button', { name: 'Renovar' })).toBeNull()
    expect(within(fila).queryByRole('button', { name: 'Finalizar' })).toBeNull()
  })

  it('exige confirmación explícita antes de renovar', async () => {
    const renovar = vi.spyOn(api, 'renovarRol').mockResolvedValue()

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = within(await screen.findByRole('alertdialog'))

    // Abrir el diálogo no ejecuta nada por sí solo.
    expect(renovar).not.toHaveBeenCalled()

    const campo = confirmacion.getByLabelText('Nuevo fin de vigencia')
    await userEvent.clear(campo)
    await userEvent.type(campo, '2028-06-30T12:00')

    await userEvent.click(confirmacion.getByRole('button', { name: 'Renovar asignación' }))

    expect(renovar).toHaveBeenCalledTimes(1)

    const [usuarioId, asignacionId, fechaHoraFin] = renovar.mock.calls[0]!
    expect(usuarioId).toBe(USUARIO_ID)
    expect(asignacionId).toBe(ASIGNACION_ID)
    // Viaja en UTC, no en hora local: el backend persiste siempre UTC (Constitución, Principio IV).
    expect(fechaHoraFin).toMatch(/Z$/)
  })

  it('cancelar cierra la confirmación sin llamar al servidor', async () => {
    const renovar = vi.spyOn(api, 'renovarRol').mockResolvedValue()

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = await screen.findByRole('alertdialog')
    await userEvent.click(within(confirmacion).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('alertdialog')).toBeNull()
    expect(renovar).not.toHaveBeenCalled()
  })

  it('impide confirmar una fecha que no sea posterior a la vigente', async () => {
    const renovar = vi.spyOn(api, 'renovarRol').mockResolvedValue()

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = within(await screen.findByRole('alertdialog'))

    const campo = confirmacion.getByLabelText('Nuevo fin de vigencia')
    await userEvent.clear(campo)
    await userEvent.type(campo, '2026-06-30T12:00')

    // Es la misma regla que el servidor aplica con 409 RENOVACION_NO_POSTERIOR; aquí solo evita el
    // viaje inútil, no sustituye la validación del backend.
    expect(confirmacion.getByRole('button', { name: 'Renovar asignación' })).toBeDisabled()
    expect(renovar).not.toHaveBeenCalled()
  })

  it('traduce el conflicto del servidor a un mensaje de negocio', async () => {
    vi.spyOn(api, 'renovarRol').mockRejectedValue(
      new ApiError({ codigo: 'RENOVACION_NO_POSTERIOR', detail: 'Conflicto' }, 409),
    )

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = within(await screen.findByRole('alertdialog'))

    const campo = confirmacion.getByLabelText('Nuevo fin de vigencia')
    await userEvent.clear(campo)
    await userEvent.type(campo, '2028-06-30T12:00')
    await userEvent.click(confirmacion.getByRole('button', { name: 'Renovar asignación' }))

    expect(
      await screen.findByText(/La nueva fecha de fin debe ser posterior a la vigente/),
    ).toBeInTheDocument()
  })

  it('informa sin revelar nada cuando la asignación queda fuera de alcance', async () => {
    // RF-077: fuera de alcance el servidor responde 404 y la interfaz se comporta como si el recurso
    // no existiera, sin nombrar compañías ni usuarios ajenos.
    vi.spyOn(api, 'renovarRol').mockRejectedValue(
      new ApiError({ codigo: 'RECURSO_NO_ENCONTRADO', detail: 'No encontrado' }, 404),
    )

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = within(await screen.findByRole('alertdialog'))

    const campo = confirmacion.getByLabelText('Nuevo fin de vigencia')
    await userEvent.clear(campo)
    await userEvent.type(campo, '2028-06-30T12:00')
    await userEvent.click(confirmacion.getByRole('button', { name: 'Renovar asignación' }))

    expect(await screen.findByText(/no pertenece a su alcance/)).toBeInTheDocument()
  })

  it('explica la limitación del rol cuando el servidor responde 403', async () => {
    vi.spyOn(api, 'renovarRol').mockRejectedValue(
      new ApiError({ codigo: 'ROL_NO_AUTORIZADO', detail: 'Prohibido' }, 403),
    )

    const fila = await abrirAsignaciones()
    await userEvent.click(within(fila).getByRole('button', { name: 'Renovar' }))

    const confirmacion = within(await screen.findByRole('alertdialog'))

    const campo = confirmacion.getByLabelText('Nuevo fin de vigencia')
    await userEvent.clear(campo)
    await userEvent.type(campo, '2028-06-30T12:00')
    await userEvent.click(confirmacion.getByRole('button', { name: 'Renovar asignación' }))

    expect(await screen.findByText(/Su rol no autoriza/)).toBeInTheDocument()
  })
})

/**
 * VF-011 (extensión): el detalle muestra el nombre de la compañía, nunca su identificador (RF-013).
 *
 * Las asignaciones solo traen `companiaId`; el nombre se resuelve con la lista de compañías. Si no puede
 * resolverse (inactiva o fuera del alcance actual), se dice "Compañía no disponible", no el UUID.
 */
describe('UsuarioDetalle — nombre de la compañía (VF-011, RF-013)', () => {
  const GLOBAL_ID = '0199b0d0-0000-7000-8000-00000000aa02'

  beforeEach(() => {
    vi.spyOn(api, 'listarRoles').mockResolvedValue([
      asignacion(),
      asignacion({
        id: GLOBAL_ID,
        rol: 'GLOBAL_ADMINISTRATOR',
        companiaId: null,
        fechaHoraInicio: '2025-01-01T00:00:00Z',
        vigente: false,
      }),
    ])
    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(companias([compania()]))
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('la pestaña Asignaciones muestra el nombre de la compañía y conserva el alcance global', async () => {
    const fila = await abrirAsignaciones()

    expect(await within(fila).findByText('Minera Propia')).toBeInTheDocument()
    expect(within(fila).queryByText(COMPANIA_ID)).toBeNull()

    const global = screen.getByText('Administrador global').closest('tr')!
    expect(within(global).getByText('Todas (alcance global)')).toBeInTheDocument()
  })

  it('el Histórico muestra el nombre de la compañía y conserva el alcance global', async () => {
    renderizar()
    await userEvent.click(screen.getByRole('tab', { name: 'Histórico' }))

    expect(await screen.findByText(/Minera Propia/)).toBeInTheDocument()
    expect(screen.getByText(/alcance global/)).toBeInTheDocument()
    expect(screen.queryByText(new RegExp(COMPANIA_ID))).toBeNull()
  })

  it('una compañía que no puede resolverse se muestra como no disponible, nunca con su UUID', async () => {
    // Inactiva o fuera del alcance actual: no figura en la lista de compañías activas del alcance.
    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(companias([]))

    const fila = await abrirAsignaciones()
    expect(await within(fila).findByText('Compañía no disponible')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: 'Histórico' }))
    expect(await screen.findByText('Compañía no disponible')).toBeInTheDocument()

    expect(screen.queryByText(new RegExp(COMPANIA_ID))).toBeNull()
  })
})
