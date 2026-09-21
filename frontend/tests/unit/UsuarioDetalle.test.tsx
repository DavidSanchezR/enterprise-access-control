import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
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

function asignacion(overrides: Partial<AsignacionRol> = {}): AsignacionRol {
  return {
    id: ASIGNACION_ID,
    usuarioId: USUARIO_ID,
    rol: 'COMPANY_ADMINISTRATOR',
    companiaId: '0199b0d0-0000-7000-8000-0000000000aa',
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
