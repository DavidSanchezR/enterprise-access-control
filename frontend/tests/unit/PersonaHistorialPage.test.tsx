import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as companiasApi from '../../src/features/companies/api'
import * as credencialesApi from '../../src/features/credentials/api'
import * as maestrosApi from '../../src/features/masters/api'
import type { Compania, PaginaCompanias } from '../../src/features/companies/api'
import * as orgUnitsApi from '../../src/features/org-units/api'
import * as api from '../../src/features/people/history/api'
import type { AsignacionCompania, ContextoOperativo } from '../../src/features/people/history/api'
import { PersonaHistorialPage } from '../../src/features/people/history/PersonaHistorialPage'

const PERSONA = '0199b0d0-9000-7000-8000-000000000001'
const CONTRATISTA = '0199b0d0-8000-7000-8000-00000000000c'
const PRINCIPAL = '0199b0d0-8000-7000-8000-00000000000a'

const ayer = new Date(Date.now() - 86_400_000).toISOString()
const dentroDeUnAno = new Date(Date.now() + 365 * 86_400_000).toISOString()
const haceDosAnos = new Date(Date.now() - 730 * 86_400_000).toISOString()
const haceUnAno = new Date(Date.now() - 365 * 86_400_000).toISOString()

function compania(id: string, nombre: string, tipo: Compania['tipoCompania']): Compania {
  return {
    id,
    nombre,
    tipoDocumentoId: '0199b0d0-0001-7000-8000-000000000001',
    numeroDocumento: id.slice(-8),
    tipoCompania: tipo,
    estado: 'ACTIVO',
  }
}

function pagina(items: Compania[]): PaginaCompanias {
  return { items, total: items.length, pagina: 1, tamañoPagina: 200 }
}

function pertenencia(overrides: Partial<AsignacionCompania> = {}): AsignacionCompania {
  return {
    id: 'p1',
    personaId: PERSONA,
    companiaId: CONTRATISTA,
    fechaHoraInicio: ayer,
    fechaHoraFin: dentroDeUnAno,
    estado: 'ACTIVA',
    motivoFin: null,
    ...overrides,
  }
}

function contexto(overrides: Partial<ContextoOperativo> = {}): ContextoOperativo {
  return {
    id: 'c1',
    personaId: PERSONA,
    companiaPrincipalId: PRINCIPAL,
    fechaHoraInicio: ayer,
    fechaHoraFin: dentroDeUnAno,
    estado: 'ACTIVO',
    motivoFin: null,
    revocadoPorPertenenciaId: null,
    ...overrides,
  }
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <MemoryRouter initialEntries={[`/personas/${PERSONA}/historial`]}>
        <Routes>
          <Route path="/personas/:personaId/historial" element={<PersonaHistorialPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('PersonaHistorialPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(
      pagina([
        compania(CONTRATISTA, 'Servicios Sur', 'CONTRATISTA'),
        compania(PRINCIPAL, 'Minera Norte', 'PRINCIPAL_MANDANTE'),
      ]),
    )

    vi.spyOn(orgUnitsApi, 'listarUnidades').mockResolvedValue([])
    vi.spyOn(api, 'listarUnidadesDelContexto').mockResolvedValue([])

    // La página incluye la sección de credenciales (Historia 9); aquí no es lo que se prueba.
    vi.spyOn(credencialesApi, 'listarCredenciales').mockResolvedValue([])
    vi.spyOn(maestrosApi, 'listarMaestro').mockResolvedValue([])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('muestra la pertenencia con su compañía y su vigencia', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])

    renderizar()

    expect(await screen.findByText('Servicios Sur')).toBeInTheDocument()
    expect(screen.getByText('ACTIVA')).toBeInTheDocument()
  })

  it('distingue el estado administrativo de la vigencia por fechas', async () => {
    // RF-063/RF-064: un registro revocado puede seguir vigente hasta una fecha futura. Mostrar solo
    // el estado daría a entender que el acceso ya se cortó.
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([
      contexto({ estado: 'INACTIVO', motivoFin: 'REVOCACION_CESE_PERTENENCIA' }),
    ])

    renderizar()

    const fila = (await screen.findByText('Minera Norte')).closest('li')!

    expect(within(fila).getByText('INACTIVO')).toBeInTheDocument()
    expect(within(fila).getByText('Vigente hoy')).toBeInTheDocument()
  })

  it('explica el motivo de fin en texto legible', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([
      pertenencia({ estado: 'FINALIZADA', motivoFin: 'CESE_PERTENENCIA' }),
    ])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])

    renderizar()

    expect(await screen.findByText('Cese de pertenencia')).toBeInTheDocument()
  })

  it('señala cuándo un contexto fue revocado en cascada', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([
      contexto({
        estado: 'INACTIVO',
        motivoFin: 'REVOCACION_CESE_PERTENENCIA',
        revocadoPorPertenenciaId: 'p1',
      }),
    ])

    renderizar()

    expect(await screen.findByText(/revocación automática en cascada/)).toBeInTheDocument()
  })

  it('solo ofrece renovar y finalizar en una pertenencia activa y vigente', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([
      pertenencia({ id: 'vigente' }),
      pertenencia({
        id: 'cerrada',
        estado: 'FINALIZADA',
        motivoFin: 'REEMPLAZO_ASIGNACION',
        fechaHoraInicio: haceDosAnos,
        fechaHoraFin: haceUnAno,
      }),
    ])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])

    renderizar()

    await screen.findByText('Cese de pertenencia').catch(() => undefined)

    // Una sola pertenencia es renovable: la cerrada no debe ofrecer esas acciones.
    expect(await screen.findAllByRole('button', { name: 'Renovar' })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: 'Finalizar' })).toHaveLength(1)
  })

  it('finaliza la pertenencia al confirmarlo', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])
    const finalizar = vi.spyOn(api, 'finalizarPertenencia').mockResolvedValue()

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Finalizar' }))

    expect(finalizar.mock.calls[0][0]).toBe(PERSONA)
    expect(finalizar.mock.calls[0][1]).toBe('p1')
  })

  it('renueva la pertenencia con la nueva fecha', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])
    const renovar = vi.spyOn(api, 'renovarPertenencia').mockResolvedValue()

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Renovar' }))

    const dialogo = within(screen.getByRole('dialog'))
    await userEvent.type(dialogo.getByLabelText('Nueva fecha de fin'), '2030-06-30')
    await userEvent.click(dialogo.getByRole('button', { name: 'Renovar' }))

    expect(renovar.mock.calls[0][1]).toBe('p1')
    expect(renovar.mock.calls[0][2]).toContain('2030-06-30')
  })

  it('explica que una pertenencia expirada ya no es renovable', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])
    vi.spyOn(api, 'renovarPertenencia').mockRejectedValue(
      new ApiError({ status: 409, detail: '…', codigo: 'PERTENENCIA_NO_RENOVABLE' }, 409),
    )

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Renovar' }))

    const dialogo = within(screen.getByRole('dialog'))
    await userEvent.type(dialogo.getByLabelText('Nueva fecha de fin'), '2030-06-30')
    await userEvent.click(dialogo.getByRole('button', { name: 'Renovar' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Registre una nueva pertenencia')
  })

  it('advierte que la renovación no afecta a lo ya existente', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([pertenencia()])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])

    renderizar()

    await userEvent.click(await screen.findByRole('button', { name: 'Renovar' }))

    // RF-073: renovar solo amplía el techo temporal; confundirlo con finalizar tendría el efecto
    // contrario.
    expect(
      within(screen.getByRole('dialog')).getByText(/no cierra la pertenencia/),
    ).toBeInTheDocument()
  })

  it('muestra un estado vacío cuando no hay pertenencias', async () => {
    vi.spyOn(api, 'listarHistorialCompanias').mockResolvedValue([])
    vi.spyOn(api, 'listarContextos').mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText('Esta persona todavía no tiene ninguna pertenencia registrada.'),
    ).toBeInTheDocument()
  })
})
