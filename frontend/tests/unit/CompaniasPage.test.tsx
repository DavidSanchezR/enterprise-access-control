import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as api from '../../src/features/companies/api'
import type { Compania, PaginaCompanias } from '../../src/features/companies/api'
import { CompaniasPage } from '../../src/features/companies/CompaniasPage'
import * as maestrosApi from '../../src/features/masters/api'

/** Tipos de documento activos del maestro (VF-001: se eligen por nombre, viaja el id). */
const TIPO_RUC = '0199b0d0-0000-7000-8000-0000000000dd'
const TIPO_DNI = '0199b0d0-0000-7000-8000-0000000000de'

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
    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([
      { id: TIPO_RUC, nombre: 'RUC', estado: 'ACTIVO' },
      { id: TIPO_DNI, nombre: 'DNI', estado: 'ACTIVO' },
    ])
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
    await screen.findByRole('option', { name: 'RUC' })
    await userEvent.selectOptions(screen.getByLabelText('Tipo de documento'), 'RUC')
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
    await screen.findByRole('option', { name: 'RUC' })
    await userEvent.selectOptions(screen.getByLabelText('Tipo de documento'), 'RUC')
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

  // --- VF-001 (post-Baseline): tipo de documento elegido del maestro, nunca por su identificador ---

  it('el tipo de documento se elige por nombre y nunca se muestra su identificador', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([]))

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Nueva compañía' }))

    const campo = screen.getByLabelText('Tipo de documento')

    // RF-013: es un selector del maestro, no un campo donde escribir el UUID.
    expect(campo.tagName).toBe('SELECT')
    await screen.findByRole('option', { name: 'RUC' })

    const textos = within(campo).getAllByRole('option').map((o) => o.textContent)
    expect(textos).toEqual(['Seleccione…', 'RUC', 'DNI'])
    expect(screen.queryByText(TIPO_RUC)).toBeNull()
    expect(screen.queryByText('Identificador del maestro de tipos de documento.')).toBeNull()
  })

  it('exige elegir un tipo de documento para crear', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearCompania')

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Nueva compañía' }))

    await userEvent.type(screen.getByLabelText('Nombre'), 'Sin tipo')
    await userEvent.type(screen.getByLabelText('Número de documento'), '77777777-7')
    await userEvent.click(screen.getByRole('button', { name: 'Crear compañía' }))

    expect(await screen.findByText('Seleccione el tipo de documento.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('al editar muestra el tipo de documento actual por su nombre y conserva su identificador', async () => {
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(pagina([compania()]))
    const actualizar = vi.spyOn(api, 'actualizarCompania').mockResolvedValue(compania())

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Editar' }))

    const campo = screen.getByLabelText('Tipo de documento')
    await screen.findByRole('option', { name: 'RUC' })

    // El valor guardado (el id de RUC) se presenta por su nombre.
    expect(campo).toHaveDisplayValue('RUC')

    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(actualizar).toHaveBeenCalledTimes(1)
    expect(actualizar.mock.calls[0][0]).toBe(compania().id)
    expect(actualizar.mock.calls[0][1].tipoDocumentoId).toBe(TIPO_RUC)
  })

  it('al editar, un tipo de documento actual no disponible exige elegir uno activo para guardar', async () => {
    // Inactivo o no resoluble: no figura entre los tipos ACTIVOS (RF-032). No se ofrece una opción
    // especial y nunca se muestra su identificador.
    const RETIRADO = '0199b0d0-0000-7000-8000-0000000000ff'
    vi.spyOn(api, 'listarCompanias').mockResolvedValue(
      pagina([compania({ tipoDocumentoId: RETIRADO })]),
    )
    const actualizar = vi.spyOn(api, 'actualizarCompania').mockResolvedValue(compania())

    renderizar()
    await userEvent.click(await screen.findByRole('button', { name: 'Editar' }))

    const campo = screen.getByLabelText('Tipo de documento')
    await screen.findByRole('option', { name: 'RUC' })

    expect(campo).toHaveDisplayValue('Seleccione…')
    expect(screen.queryByText(RETIRADO)).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText('Seleccione el tipo de documento.')).toBeInTheDocument()
    expect(actualizar).not.toHaveBeenCalled()

    await userEvent.selectOptions(campo, 'DNI')
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(actualizar).toHaveBeenCalledTimes(1)
    expect(actualizar.mock.calls[0][1].tipoDocumentoId).toBe(TIPO_DNI)
  })
})
