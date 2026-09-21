import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../src/features/companies/api'
import type { Compania, PaginaCompanias } from '../../src/features/companies/api'
import { CompaniasPage } from '../../src/features/companies/CompaniasPage'

function compania(overrides: Partial<Compania> = {}): Compania {
  return {
    id: '0199b0d0-0000-7000-8000-000000000001',
    nombre: 'Minera Los Andes',
    tipoDocumentoId: '0199b0d0-0000-7000-8000-0000000000dd',
    numeroDocumento: '76543210-9',
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    zonaHorariaIana: 'America/Lima',
    ...overrides,
  }
}

function pagina(items: Compania[]): PaginaCompanias {
  return { items, total: items.length, pagina: 1, tamañoPagina: 20 }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <CompaniasPage />
    </QueryClientProvider>,
  )
}

describe('CompaniasPage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('muestra la clasificación de cada compañía como texto legible', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(
      pagina([
        compania(),
        compania({ id: 'c', nombre: 'Servicios Sur', tipoCompania: 'CONTRATISTA' }),
      ]),
    )

    renderizar()

    const tabla = (await screen.findByText('Minera Los Andes')).closest('table')!

    // RF-042: la clasificación siempre visible, no deducible solo por color.
    expect(within(tabla).getByText('Principal mandante')).toBeInTheDocument()
    expect(within(tabla).getByText('Contratista')).toBeInTheDocument()
  })

  it('filtra por tipo de compañía y vuelve a la primera página', async () => {
    const listar = vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([compania()]))

    renderizar()
    await screen.findByText('Minera Los Andes')

    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'CONTRATISTA')

    expect(listar).toHaveBeenLastCalledWith(
      expect.objectContaining({ tipoCompania: 'CONTRATISTA', pagina: 1 }),
    )
  })

  it('busca por nombre o número de documento', async () => {
    const listar = vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([compania()]))

    renderizar()
    await screen.findByText('Minera Los Andes')

    await userEvent.type(screen.getByLabelText('Buscar'), 'Andes')

    expect(listar).toHaveBeenLastCalledWith(expect.objectContaining({ texto: 'Andes' }))
  })

  it('solo ofrece gestionar principales a las compañías contratistas', async () => {
    // Nombres deliberadamente distintos de las etiquetas de clasificación: si la compañía se
    // llamara "Contratista", la consulta por texto no distinguiría el nombre de la etiqueta.
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(
      pagina([
        compania({ id: 'p', nombre: 'Faena Norte', tipoCompania: 'PRINCIPAL_MANDANTE' }),
        compania({ id: 'c', nombre: 'Servicios Sur', tipoCompania: 'CONTRATISTA' }),
      ]),
    )

    renderizar()

    const filaContratista = (await screen.findByText('Servicios Sur')).closest('tr')!
    const filaPrincipal = screen.getByText('Faena Norte').closest('tr')!

    // RF-051: la relación se declara desde la Contratista hacia sus Principales.
    expect(within(filaContratista).getByRole('button', { name: 'Principales' })).toBeInTheDocument()
    expect(within(filaPrincipal).queryByRole('button', { name: 'Principales' })).toBeNull()
  })

  it('crea una compañía con su clasificación', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearCompania').mockResolvedValue(compania())

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nueva compañía' }))

    await userEvent.type(screen.getByLabelText('Nombre'), 'Constructora Boreal')
    await userEvent.type(
      screen.getByLabelText('Tipo de documento'),
      '0199b0d0-0000-7000-8000-0000000000dd',
    )
    await userEvent.type(screen.getByLabelText('Número de documento'), '77777777-7')
    await userEvent.selectOptions(screen.getByLabelText('Clasificación'), 'CONTRATISTA')

    await userEvent.click(screen.getByRole('button', { name: 'Crear compañía' }))

    expect(crear.mock.calls[0][0]).toEqual({
      nombre: 'Constructora Boreal',
      tipoDocumentoId: '0199b0d0-0000-7000-8000-0000000000dd',
      numeroDocumento: '77777777-7',
      tipoCompania: 'CONTRATISTA',
      estado: 'ACTIVO',
      // RF-080: una CONTRATISTA no declara zona propia; el formulario la envía como null.
      zonaHorariaIana: null,
    })
  })

  it('explica el conflicto cuando el documento ya está registrado', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([]))
    vi.spyOn(api, 'crearCompania').mockRejectedValue(
      new (await import('../../src/lib/apiClient')).ApiError(
        { status: 409, detail: 'Ya existe…', codigo: 'DOCUMENTO_YA_REGISTRADO' },
        409,
      ),
    )

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nueva compañía' }))
    await userEvent.type(screen.getByLabelText('Nombre'), 'Duplicada')
    await userEvent.type(
      screen.getByLabelText('Tipo de documento'),
      '0199b0d0-0000-7000-8000-0000000000dd',
    )
    await userEvent.type(screen.getByLabelText('Número de documento'), '77777777-7')
    await userEvent.click(screen.getByRole('button', { name: 'Crear compañía' }))

    // Se ramifica por el código de negocio, no por el texto del servidor.
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ya existe una compañía con ese tipo y número de documento.',
    )
  })

  it('no envía el alta si faltan datos obligatorios', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearCompania')

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Nueva compañía' }))
    await userEvent.click(screen.getByRole('button', { name: 'Crear compañía' }))

    expect(await screen.findByText('Indique el nombre.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })
})
