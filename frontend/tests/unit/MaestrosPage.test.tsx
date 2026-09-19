import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../src/features/masters/api'
import type { MasterItem } from '../../src/features/masters/api'
import { MaestrosPage } from '../../src/features/masters/MaestrosPage'

function item(overrides: Partial<MasterItem> = {}): MasterItem {
  return {
    id: '0199b0d0-0001-7000-8000-000000000001',
    nombre: 'DNI',
    estado: 'ACTIVO',
    ...overrides,
  }
}

function renderizar(ruta = '/maestros/tipos-documento'): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path="/maestros/:catalogo" element={<MaestrosPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('MaestrosPage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lista el catálogo que indica la ruta', async () => {
    const listar = vi.spyOn(api, 'listarMaestro').mockResolvedValue([item()])

    renderizar('/maestros/tipos-sangre')

    await screen.findByRole('table')

    expect(listar.mock.calls[0][0]).toBe('tipos-sangre')
  })

  it('ofrece los cinco catálogos como pestañas', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([])

    renderizar()

    const navegacion = await screen.findByRole('navigation', { name: 'Catálogos maestros' })

    for (const titulo of [
      'Tipos de documento',
      'Tipos de sangre',
      'Géneros',
      'Tipos de persona',
      'Tipos de credencial',
    ]) {
      expect(within(navegacion).getByRole('link', { name: titulo })).toBeInTheDocument()
    }
  })

  it('muestra el estado de cada valor como texto', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([
      item({ id: 'a', nombre: 'DNI', estado: 'ACTIVO' }),
      item({ id: 'b', nombre: 'Libreta Militar', estado: 'INACTIVO' }),
    ])

    renderizar()

    const tabla = (await screen.findByText('Libreta Militar')).closest('table')!

    expect(within(tabla).getByText('ACTIVO')).toBeInTheDocument()
    expect(within(tabla).getByText('INACTIVO')).toBeInTheDocument()
  })

  it('desactiva un valor sin borrarlo', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([item()])
    const actualizar = vi
      .spyOn(api, 'actualizarMaestro')
      .mockResolvedValue(item({ estado: 'INACTIVO' }))

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Desactivar' }))

    // RF-032: desactivar es un cambio de estado, nunca una eliminación.
    expect(actualizar.mock.calls[0][2]).toEqual({ nombre: 'DNI', estado: 'INACTIVO' })
  })

  it('ofrece reactivar un valor inactivo', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([item({ estado: 'INACTIVO' })])
    const actualizar = vi.spyOn(api, 'actualizarMaestro').mockResolvedValue(item())

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Reactivar' }))

    expect(actualizar.mock.calls[0][2]).toEqual({ nombre: 'DNI', estado: 'ACTIVO' })
  })

  it('filtra por estado', async () => {
    const listar = vi.spyOn(api, 'listarMaestro').mockResolvedValue([item()])

    renderizar()
    await screen.findByRole('table')

    await userEvent.selectOptions(screen.getByLabelText('Estado'), 'INACTIVO')

    expect(listar.mock.calls.at(-1)![1]).toBe('INACTIVO')
  })

  it('crea un valor nuevo en el catálogo activo', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([])
    const crear = vi.spyOn(api, 'crearMaestro').mockResolvedValue(item({ nombre: 'Pasaporte' }))

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo valor' }))
    await userEvent.type(screen.getByLabelText('Nombre'), 'Pasaporte')
    await userEvent.click(screen.getByRole('button', { name: 'Crear valor' }))

    expect(crear.mock.calls[0][0]).toBe('tipos-documento')
    expect(crear.mock.calls[0][1]).toEqual({ nombre: 'Pasaporte', estado: 'ACTIVO' })
  })

  it('respeta el límite de longitud propio de cada catálogo', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([])
    const crear = vi.spyOn(api, 'crearMaestro')

    // Tipo de sangre admite 10 caracteres, no los 200 del máximo genérico del contrato.
    renderizar('/maestros/tipos-sangre')

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo valor' }))

    const campo = screen.getByLabelText('Nombre')
    expect(campo).toHaveAttribute('maxLength', '10')

    await userEvent.click(screen.getByRole('button', { name: 'Crear valor' }))

    expect(await screen.findByText('Indique el nombre.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('explica el conflicto cuando el nombre ya existe', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(api, 'listarMaestro').mockResolvedValue([])
    vi.spyOn(api, 'crearMaestro').mockRejectedValue(
      new ApiError({ status: 409, detail: 'Ya existe…', codigo: 'NOMBRE_YA_REGISTRADO' }, 409),
    )

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nuevo valor' }))
    await userEvent.type(screen.getByLabelText('Nombre'), 'DNI')
    await userEvent.click(screen.getByRole('button', { name: 'Crear valor' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ya existe un valor con ese nombre en este catálogo.',
    )
  })

  it('muestra un estado vacío explicativo', async () => {
    vi.spyOn(api, 'listarMaestro').mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText('Todavía no hay valores en tipos de documento.'),
    ).toBeInTheDocument()
  })
})
