import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as orgUnitsApi from '../../src/features/org-units/api'
import type { NodoArbolUnidad, UnidadOrganizativa } from '../../src/features/org-units/api'
import * as companiasApi from '../../src/features/companies/api'
import { AsignacionUnidadOrganizativa } from '../../src/features/people/AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa'
import * as api from '../../src/features/people/history/api'
import type { ContextoOperativo } from '../../src/features/people/history/api'

const PERSONA = '0199b0d0-9000-7000-8000-000000000001'
const PRINCIPAL_A = '0199b0d0-8000-7000-8000-00000000000a'
const PRINCIPAL_B = '0199b0d0-8000-7000-8000-00000000000b'
const UNIDAD_A = '0199b0d0-7000-7000-8000-00000000000a'

const ayer = new Date(Date.now() - 86_400_000).toISOString()
const dentroDeUnAno = new Date(Date.now() + 365 * 86_400_000).toISOString()

function contexto(overrides: Partial<ContextoOperativo> = {}): ContextoOperativo {
  return {
    id: 'c1',
    personaId: PERSONA,
    companiaPrincipalId: PRINCIPAL_A,
    fechaHoraInicio: ayer,
    fechaHoraFin: dentroDeUnAno,
    estado: 'ACTIVO',
    motivoFin: null,
    revocadoPorPertenenciaId: null,
    ...overrides,
  }
}

function nodoArbol(id: string, nombre: string): NodoArbolUnidad {
  return { id, nombre, unidadSuperiorId: null, estado: 'ACTIVO', hijos: [] }
}

function unidad(id: string, nombre: string): UnidadOrganizativa {
  return {
    id,
    nombre,
    unidadSuperiorId: null,
    companiaPrincipalId: PRINCIPAL_A,
    estado: 'ACTIVO',
  }
}

/**
 * Elige la unidad sobre el arbol, esperando a que sus nodos lleguen.
 *
 * CS-021 exige elegir sobre el arbol y no sobre una lista plana: el selector es un `treeitem`, no
 * una opcion de `<select>`.
 */
async function elegirUnidadAsync(): Promise<void> {
  const nodo = await screen.findByRole('treeitem', { name: /Planta Norte/ })
  await userEvent.click(nodo)
}

function renderizar(ctx = contexto()): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <AsignacionUnidadOrganizativa personaId={PERSONA} contexto={ctx} />
    </QueryClientProvider>,
  )
}

describe('AsignacionUnidadOrganizativa', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.spyOn(api, 'listarUnidadesDelContexto').mockResolvedValue([])
    vi.spyOn(orgUnitsApi, 'listarUnidades').mockResolvedValue([unidad(UNIDAD_A, 'Planta Norte')])
    vi.spyOn(orgUnitsApi, 'obtenerArbol').mockResolvedValue([nodoArbol(UNIDAD_A, 'Planta Norte')])
    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue({
      items: [],
      total: 0,
      pagina: 1,
      tamañoPagina: 200,
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('no ofrece elegir compañía principal: la fija el contexto', async () => {
    renderizar()

    await screen.findByRole('treeitem', { name: /Planta Norte/ })

    // Caso A/B ya quedó resuelto al abrir el contexto. Un selector aquí permitiría proponer una
    // unidad de otra Principal, que el servidor rechazaría (RF-055).
    expect(screen.queryByLabelText(/compañía principal/i)).not.toBeInTheDocument()
  })

  it('solo lista unidades de la compañía principal del contexto', async () => {
    const listar = vi.spyOn(orgUnitsApi, 'obtenerArbol')

    renderizar()

    await screen.findByRole('treeitem', { name: /Planta Norte/ })

    expect(listar.mock.calls[0][0]).toBe(PRINCIPAL_A)
  })

  it('consulta el árbol de la principal correcta en otro contexto', async () => {
    const listar = vi.spyOn(orgUnitsApi, 'obtenerArbol')

    renderizar(contexto({ id: 'c2', companiaPrincipalId: PRINCIPAL_B }))

    await screen.findByRole('treeitem', { name: /Planta Norte/ })

    expect(listar.mock.calls[0][0]).toBe(PRINCIPAL_B)
  })

  it('preselecciona las fechas dentro de la vigencia del contexto', async () => {
    renderizar()

    const desde = await screen.findByLabelText('Desde')
    const hasta = screen.getByLabelText('Hasta')

    // Así la propuesta por defecto ya cumple la contención temporal (RF-072).
    expect(desde).toHaveValue(ayer.slice(0, 10))
    expect(hasta).toHaveValue(dentroDeUnAno.slice(0, 10))
  })

  it('asigna la unidad con las fechas indicadas', async () => {
    const asignar = vi.spyOn(api, 'asignarUnidad').mockResolvedValue({
      id: 'a1',
      personaId: PERSONA,
      contextoOperativoId: 'c1',
      unidadOrganizativaId: UNIDAD_A,
      fechaHoraInicio: ayer,
      fechaHoraFin: dentroDeUnAno,
      estado: 'ACTIVO',
      motivoFin: null,
      revocadoPorPertenenciaId: null,
    })

    renderizar()

    await elegirUnidadAsync()

    await userEvent.click(screen.getByRole('button', { name: 'Asignar unidad' }))

    expect(asignar.mock.calls[0][1]).toBe('c1')
    expect(asignar.mock.calls[0][2].unidadOrganizativaId).toBe(UNIDAD_A)
  })

  it('traduce el rechazo por contención temporal', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(api, 'asignarUnidad').mockRejectedValue(
      new ApiError({ status: 409, detail: '…', codigo: 'FUERA_DE_CONTENCION_TEMPORAL' }, 409),
    )

    renderizar()

    await elegirUnidadAsync()

    await userEvent.click(screen.getByRole('button', { name: 'Asignar unidad' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'dentro de la vigencia de la pertenencia',
    )
  })

  it('traduce el rechazo por unidad de otra principal', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(api, 'asignarUnidad').mockRejectedValue(
      new ApiError({ status: 409, detail: '…', codigo: 'COMPANIA_DEBE_SER_PRINCIPAL' }, 409),
    )

    renderizar()

    await elegirUnidadAsync()

    await userEvent.click(screen.getByRole('button', { name: 'Asignar unidad' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('otra compañía principal')
  })

  it('un contexto inactivo no admite nuevas asignaciones', async () => {
    renderizar(contexto({ estado: 'INACTIVO', motivoFin: 'REVOCACION_CESE_PERTENENCIA' }))

    await screen.findByText(/todavía no tiene unidad organizativa asignada/)

    // Ofrecer el formulario invitaría a una operación que el servidor rechazará.
    expect(screen.queryByRole('button', { name: 'Asignar unidad' })).not.toBeInTheDocument()
  })

  it('advierte que la fecha de fin es obligatoria', async () => {
    renderizar()

    // RF-071: no existe asignación de vigencia indefinida.
    expect(
      await screen.findByText(/no existe asignación de vigencia indefinida/),
    ).toBeInTheDocument()
  })
})
