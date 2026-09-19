import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as maestrosApi from '../../src/features/masters/api'
import type { MasterItem } from '../../src/features/masters/api'
import * as api from '../../src/features/people/api'
import type { PaginaPersonas, Persona } from '../../src/features/people/api'
import { PersonasPage } from '../../src/features/people/PersonasPage'

const DNI = '0199b0d0-0001-7000-8000-000000000001'
const MASCULINO = '0199b0d0-0003-7000-8000-000000000001'
const O_POSITIVO = '0199b0d0-0002-7000-8000-000000000001'

function persona(overrides: Partial<Persona> = {}): Persona {
  return {
    id: '0199b0d0-9000-7000-8000-000000000001',
    nombres: 'Ana',
    apellidos: 'Quispe',
    fechaNacimiento: '1990-05-20',
    tipoDocumentoId: DNI,
    numeroDocumento: '45678912',
    generoId: MASCULINO,
    correoElectronico: 'ana.quispe@empresa.cl',
    tipoSangreId: O_POSITIVO,
    contactoEmergencia: 'María Quispe',
    numeroEmergencia: '+51 999 111 222',
    ...overrides,
  }
}

function pagina(items: Persona[]): PaginaPersonas {
  return { items, total: items.length, pagina: 1, tamañoPagina: 20 }
}

function maestro(id: string, nombre: string): MasterItem {
  return { id, nombre, estado: 'ACTIVO' }
}

/**
 * Consultas acotadas al diálogo abierto.
 *
 * La página y el formulario comparten nombres accesibles ("Registrar persona", las opciones de los
 * selectores). En un navegador real el `<dialog>` modal inertiza el fondo y no hay ambigüedad; el
 * polyfill de jsdom no lo hace, así que la prueba acota explícitamente.
 */
function dialogo() {
  return within(screen.getByRole('dialog'))
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  // La fila enlaza al histórico de la persona, así que el componente necesita un enrutador.
  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter>
        <PersonasPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('PersonasPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(maestrosApi, 'listarMaestro').mockImplementation((ruta) => {
      if (ruta === 'tipos-documento') {
        return Promise.resolve([maestro(DNI, 'DNI')])
      }
      if (ruta === 'generos') {
        return Promise.resolve([maestro(MASCULINO, 'Masculino')])
      }
      return Promise.resolve([maestro(O_POSITIVO, 'O+')])
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('lista las personas con su documento legible', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([persona()]))

    renderizar()

    const fila = (await screen.findByText('Quispe, Ana')).closest('tr')!

    // El tipo de documento se muestra por su nombre, no por su identificador.
    expect(within(fila).getByText(/DNI/)).toBeInTheDocument()
    expect(within(fila).getByText('45678912')).toBeInTheDocument()
  })

  it('nunca muestra el identificador técnico de la persona', async () => {
    const registro = persona()
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([registro]))

    renderizar()

    await screen.findByText('Quispe, Ana')

    // RF-013: el Id es interno; la persona se identifica por su documento.
    expect(screen.queryByText(registro.id)).not.toBeInTheDocument()
  })

  it('explica que la lista está limitada al alcance cuando no hay resultados', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText(/pertenencia vigente a una compañía de su alcance/),
    ).toBeVisible()
  })

  it('busca por texto y vuelve a la primera página', async () => {
    const buscar = vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([persona()]))

    renderizar()
    await screen.findByText('Quispe, Ana')

    await userEvent.type(screen.getByLabelText('Buscar'), 'Quispe')

    expect(buscar).toHaveBeenLastCalledWith(expect.objectContaining({ texto: 'Quispe', pagina: 1 }))
  })

  it('filtra por tipo de documento', async () => {
    const buscar = vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([persona()]))

    renderizar()
    await screen.findByText('Quispe, Ana')
    await screen.findByRole('option', { name: 'DNI' })

    await userEvent.selectOptions(screen.getByLabelText('Tipo de documento'), DNI)

    expect(buscar).toHaveBeenLastCalledWith(
      expect.objectContaining({ tipoDocumentoId: DNI, pagina: 1 }),
    )
  })

  it('registra una persona con los diez campos obligatorios', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearPersona').mockResolvedValue(persona())

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Registrar persona' }))
    await within(screen.getByRole('dialog')).findByRole('option', { name: 'DNI' })

    const d = dialogo()
    await userEvent.type(d.getByLabelText('Nombres'), 'Rosa')
    await userEvent.type(d.getByLabelText('Apellidos'), 'Mamani')
    await userEvent.selectOptions(d.getByLabelText('Tipo de documento'), DNI)
    await userEvent.type(d.getByLabelText('Número de documento'), '11223344')
    await userEvent.type(d.getByLabelText('Fecha de nacimiento'), '1988-03-15')
    await userEvent.selectOptions(d.getByLabelText('Género'), MASCULINO)
    await userEvent.type(d.getByLabelText('Correo electrónico'), 'rosa@empresa.cl')
    await userEvent.selectOptions(d.getByLabelText('Tipo de sangre'), O_POSITIVO)
    await userEvent.type(d.getByLabelText('Contacto de emergencia'), 'Luis Mamani')
    await userEvent.type(d.getByLabelText('Número de emergencia'), '+51 988 777 666')

    await userEvent.click(d.getByRole('button', { name: 'Registrar persona' }))

    expect(crear.mock.calls[0][0]).toEqual({
      nombres: 'Rosa',
      apellidos: 'Mamani',
      fechaNacimiento: '1988-03-15',
      tipoDocumentoId: DNI,
      numeroDocumento: '11223344',
      generoId: MASCULINO,
      correoElectronico: 'rosa@empresa.cl',
      tipoSangreId: O_POSITIVO,
      contactoEmergencia: 'Luis Mamani',
      numeroEmergencia: '+51 988 777 666',
    })
  })

  it('no registra una persona con campos obligatorios vacíos', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([]))
    const crear = vi.spyOn(api, 'crearPersona')

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Registrar persona' }))
    await userEvent.click(dialogo().getByRole('button', { name: 'Registrar persona' }))

    // RF-012: no se admite un registro parcial.
    expect(await screen.findByText('Indique los nombres.')).toBeInTheDocument()
    expect(screen.getByText('Indique el contacto de emergencia.')).toBeInTheDocument()
    expect(screen.getByText('Seleccione el tipo de sangre.')).toBeInTheDocument()
    expect(crear).not.toHaveBeenCalled()
  })

  it('rechaza una fecha de nacimiento futura', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([]))

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Registrar persona' }))
    await userEvent.type(dialogo().getByLabelText('Fecha de nacimiento'), '2999-01-01')
    await userEvent.click(dialogo().getByRole('button', { name: 'Registrar persona' }))

    expect(
      await screen.findByText('La fecha de nacimiento debe ser anterior a hoy.'),
    ).toBeInTheDocument()
  })

  it('explica el conflicto cuando el documento ya está registrado', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([persona()]))
    vi.spyOn(api, 'actualizarPersona').mockRejectedValue(
      new ApiError({ status: 409, detail: 'Ya existe…', codigo: 'DOCUMENTO_YA_REGISTRADO' }, 409),
    )

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Editar' }))
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Ya existe una persona con ese tipo y número de documento.',
    )
  })

  it('precarga los datos al editar una persona', async () => {
    vi.spyOn(api, 'buscarPersonas').mockResolvedValue(pagina([persona()]))

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Editar' }))

    expect(screen.getByLabelText('Nombres')).toHaveValue('Ana')
    expect(screen.getByLabelText('Número de documento')).toHaveValue('45678912')
    expect(screen.getByLabelText('Fecha de nacimiento')).toHaveValue('1990-05-20')
  })
})
