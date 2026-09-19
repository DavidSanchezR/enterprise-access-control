import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as tiposApi from '../../src/features/area-access/TiposPersonaPorArea/api'
import { TiposPersonaPorArea } from '../../src/features/area-access/TiposPersonaPorArea/TiposPersonaPorArea'
import * as maestrosApi from '../../src/features/masters/api'
import type { MasterItem } from '../../src/features/masters/api'

const AREA = '0199b0d0-0000-7000-8000-0000000000a1'
const TRABAJADOR = '0199b0d0-0000-7000-8000-0000000000b1'
const VISITANTE = '0199b0d0-0000-7000-8000-0000000000c1'

function tipo(id: string, nombre: string): MasterItem {
  return { id, nombre, estado: 'ACTIVO' }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <TiposPersonaPorArea areaId={AREA} nombreArea="Planta Concentradora" />
    </QueryClientProvider>,
  )
}

describe('TiposPersonaPorArea', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([
      tipo(TRABAJADOR, 'Trabajador'),
      tipo(VISITANTE, 'Visitante'),
    ])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('solo ofrece tipos de persona activos', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([])

    renderizar()

    expect(await screen.findByLabelText('Trabajador')).toBeInTheDocument()

    // RF-032: el servidor rechaza un tipo INACTIVO, así que ofrecerlo sería proponer un fallo.
    expect(vi.mocked(maestrosApi.listarMaestro).mock.calls[0]).toEqual(['tipos-persona', 'ACTIVO'])
  })

  it('marca los tipos ya autorizados en el área', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([TRABAJADOR])

    renderizar()

    expect(await screen.findByLabelText('Trabajador')).toBeChecked()
    expect(screen.getByLabelText('Visitante')).not.toBeChecked()
  })

  it('no escribe nada al marcar una casilla: guarda el conjunto completo de una vez', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([TRABAJADOR])
    const reemplazar = vi
      .spyOn(tiposApi, 'reemplazarTiposPersonaDeArea')
      .mockResolvedValue(undefined)

    renderizar()

    await userEvent.click(await screen.findByLabelText('Visitante'))

    expect(reemplazar).not.toHaveBeenCalled()

    await userEvent.click(screen.getByRole('button', { name: 'Guardar autorizaciones' }))

    expect(reemplazar).toHaveBeenCalledWith(AREA, [TRABAJADOR, VISITANTE])
  })

  it('desmarcar y guardar retira la autorización', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([TRABAJADOR, VISITANTE])
    const reemplazar = vi
      .spyOn(tiposApi, 'reemplazarTiposPersonaDeArea')
      .mockResolvedValue(undefined)

    renderizar()

    await userEvent.click(await screen.findByLabelText('Trabajador'))
    await userEvent.click(screen.getByRole('button', { name: 'Guardar autorizaciones' }))

    expect(reemplazar).toHaveBeenCalledWith(AREA, [VISITANTE])
  })

  it('advierte cuando el área no autoriza a ningún tipo', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([])

    renderizar()

    // Default-deny: conviene que quien administra vea la consecuencia, no un panel en blanco.
    expect(
      await screen.findByText('Ningún tipo autorizado: nadie podrá acceder a esta área.'),
    ).toBeInTheDocument()
  })

  it('permite vaciar el conjunto por completo', async () => {
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([TRABAJADOR])
    const reemplazar = vi
      .spyOn(tiposApi, 'reemplazarTiposPersonaDeArea')
      .mockResolvedValue(undefined)

    renderizar()

    await userEvent.click(await screen.findByLabelText('Trabajador'))
    await userEvent.click(screen.getByRole('button', { name: 'Guardar autorizaciones' }))

    expect(reemplazar).toHaveBeenCalledWith(AREA, [])
  })

  it('sigue mostrando un tipo asociado que se inactivó después', async () => {
    const retirado = '0199b0d0-0000-7000-8000-0000000000dd'

    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([retirado])

    renderizar()

    // No viene en el catálogo filtrado por ACTIVO; ocultarlo lo retiraría sin que nadie lo decida.
    expect(await screen.findByLabelText(`${retirado} (inactivo)`)).toBeChecked()
  })

  it('avisa cuando el catálogo de tipos de persona está vacío', async () => {
    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([])
    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([])

    renderizar()

    // El catálogo no se entrega con semilla (RF-010): el estado vacío es normal al empezar.
    expect(
      await screen.findByText(
        'No hay tipos de persona activos en el catálogo. Créelos en Datos maestros para poder autorizarlos aquí.',
      ),
    ).toBeInTheDocument()
  })

  it('muestra el error del servidor al guardar', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(tiposApi, 'listarTiposPersonaDeArea').mockResolvedValue([])
    vi.spyOn(tiposApi, 'reemplazarTiposPersonaDeArea').mockRejectedValue(
      new ApiError(
        {
          status: 400,
          detail: 'Tipos de persona inexistentes o inactivos.',
          codigo: 'VALOR_MAESTRO_INACTIVO',
        },
        400,
      ),
    )

    renderizar()

    await userEvent.click(await screen.findByLabelText('Trabajador'))
    await userEvent.click(screen.getByRole('button', { name: 'Guardar autorizaciones' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Tipos de persona inexistentes o inactivos.',
    )
  })
})
