import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../src/lib/apiClient'
import * as maestrosApi from '../../src/features/masters/api'
import * as historialApi from '../../src/features/people/history/api'
import type { AsignacionCompania } from '../../src/features/people/history/api'
import * as perfilesApi from '../../src/features/people/perfiles/api'
import { PerfilesPersona } from '../../src/features/people/perfiles/PerfilesPersona'

const PERSONA = '0199b0d0-0000-7000-8000-0000000000f1'
const TIPO = '0199b0d0-0000-7000-8000-0000000000a7'
const TIPO_RETIRADO = '0199b0d0-0000-7000-8000-0000000000a9'

const DIA = 24 * 60 * 60 * 1000

/** Pertenencia normalizada a días completos en UTC, como la persiste el servidor. */
function pertenencia(desdeDias: number, hastaDias: number): AsignacionCompania {
  const inicio = new Date(Date.now() + desdeDias * DIA).toISOString().slice(0, 10)
  const fin = new Date(Date.now() + hastaDias * DIA).toISOString().slice(0, 10)

  return {
    id: '0199b0d0-0000-7000-8000-0000000000b1',
    personaId: PERSONA,
    companiaId: '0199b0d0-0000-7000-8000-0000000000c1',
    fechaHoraInicio: `${inicio}T00:00:00.000Z`,
    fechaHoraFin: `${fin}T23:59:59.999Z`,
    estado: 'ACTIVA',
    motivoFin: null,
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <PerfilesPersona personaId={PERSONA} />
    </QueryClientProvider>,
  )
}

async function completarFormulario(desde: string, hasta: string): Promise<void> {
  await screen.findByRole('option', { name: 'Trabajador' })
  await userEvent.selectOptions(screen.getByLabelText('Perfil'), TIPO)
  fireEvent.change(screen.getByLabelText('Desde'), { target: { value: desde } })
  fireEvent.change(screen.getByLabelText('Hasta'), { target: { value: hasta } })
}

/**
 * Perfiles de persona frente a RF-082 (cambio post-Baseline VF-007).
 *
 * La interfaz solo orienta: límites de fecha a partir de la pertenencia vigente, aviso si no la hay y
 * un mensaje propio para cada rechazo del servidor. La regla la aplica siempre el backend.
 */
describe('PerfilesPersona', () => {
  beforeEach(() => {
    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([
      { id: TIPO, nombre: 'Trabajador', estado: 'ACTIVO' },
    ])
    vi.spyOn(perfilesApi, 'listarPerfiles').mockResolvedValue([])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('limita las fechas a la pertenencia vigente', async () => {
    const vigente = pertenencia(-30, 365)
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([vigente])

    renderizar()

    const minimo = vigente.fechaHoraInicio.slice(0, 10)
    const maximo = vigente.fechaHoraFin.slice(0, 10)

    expect(
      await screen.findByText(
        `Debe quedar dentro de la pertenencia vigente: del ${minimo} al ${maximo}.`,
      ),
    ).toBeInTheDocument()

    for (const etiqueta of ['Desde', 'Hasta']) {
      expect(screen.getByLabelText(etiqueta)).toHaveAttribute('min', minimo)
      expect(screen.getByLabelText(etiqueta)).toHaveAttribute('max', maximo)
    }
  })

  it('sin pertenencia vigente avisa y no permite asignar', async () => {
    // Solo una pertenencia ya vencida: la vigencia se decide por fechas, no por estado.
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenencia(-400, -10)])
    const asignar = vi.spyOn(perfilesApi, 'asignarPerfil')

    renderizar()

    expect(await screen.findByText(/no tiene una pertenencia vigente/)).toBeInTheDocument()

    await completarFormulario('2026-10-01', '2026-12-31')

    expect(screen.getByRole('button', { name: 'Asignar perfil' })).toBeDisabled()
    expect(asignar).not.toHaveBeenCalled()
  })

  it('si el historial no puede leerse no bloquea: decide el servidor', async () => {
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockRejectedValue(
      new ApiError({ status: 404, codigo: 'RECURSO_NO_ENCONTRADO' }, 404),
    )

    renderizar()
    await completarFormulario('2026-10-01', '2026-12-31')

    expect(screen.queryByText(/no tiene una pertenencia vigente/)).not.toBeInTheDocument()
    expect(screen.getByLabelText('Hasta')).not.toHaveAttribute('max')
    expect(screen.getByRole('button', { name: 'Asignar perfil' })).toBeEnabled()
  })

  it.each([
    [
      'FUERA_DE_CONTENCION_TEMPORAL',
      409,
      'La vigencia del perfil debe quedar dentro de la pertenencia vigente de la persona.',
    ],
    [
      'SIN_PERTENENCIA_VIGENTE',
      400,
      'La persona no tiene una pertenencia vigente: registre o renueve su pertenencia antes de asignarle un perfil.',
    ],
  ])('muestra un mensaje propio para %s', async (codigo, status, mensaje) => {
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenencia(-30, 365)])
    vi.spyOn(perfilesApi, 'asignarPerfil').mockRejectedValue(
      new ApiError({ status, codigo, detail: 'texto técnico del servidor' }, status),
    )

    renderizar()
    await completarFormulario('2026-10-01', '2026-12-31')
    await userEvent.click(screen.getByRole('button', { name: 'Asignar perfil' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(mensaje)
  })

  it('nunca muestra el identificador de un tipo de persona que no puede resolverse', async () => {
    vi.spyOn(historialApi, 'listarHistorialCompanias').mockResolvedValue([pertenencia(-30, 365)])
    vi.spyOn(perfilesApi, 'listarPerfiles').mockResolvedValue([
      {
        id: '0199b0d0-0000-7000-8000-0000000000d1',
        personaId: PERSONA,
        tipoPersonaId: TIPO_RETIRADO,
        fechaHoraInicio: '2026-09-01T00:00:00.000Z',
        fechaHoraFin: '2026-12-31T23:59:59.999Z',
        estado: 'ACTIVO',
      },
    ])

    renderizar()

    // RF-013: el respaldo es un texto, nunca el UUID.
    expect(await screen.findByText('Perfil no disponible')).toBeInTheDocument()
    expect(screen.queryByText(TIPO_RETIRADO)).not.toBeInTheDocument()
  })
})
