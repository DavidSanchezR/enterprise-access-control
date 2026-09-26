import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania, PaginaCompanias } from '../../src/features/companies/api'
import * as unidadesApi from '../../src/features/org-units/api'
import type { NodoArbolUnidad, UnidadOrganizativa } from '../../src/features/org-units/api'
import { UnidadesOrganizativasPage } from '../../src/features/org-units/UnidadesOrganizativasPage'

const P1 = '0199b0d0-0000-7000-8000-00000000000a'
const P2 = '0199b0d0-0000-7000-8000-00000000000b'

function principal(id: string, nombre: string): Compania {
  return {
    id,
    nombre,
    tipoDocumentoId: '0199b0d0-0000-7000-8000-0000000000dd',
    numeroDocumento: id.slice(-8),
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
  }
}

function paginaPrincipales(items: Compania[]): PaginaCompanias {
  return { items, total: items.length, pagina: 1, tamañoPagina: 200 }
}

function nodo(id: string, nombre: string, hijos: NodoArbolUnidad[] = []): NodoArbolUnidad {
  return { id, nombre, estado: 'ACTIVO', hijos }
}

/**
 * Elige una compañía principal esperando primero a que el selector tenga sus opciones.
 *
 * Las principales llegan de una consulta asíncrona: sin la espera, `selectOptions` actuaría sobre
 * un `<select>` que todavía solo contiene el marcador de posición.
 */
async function elegirPrincipal(id: string, nombre: string): Promise<void> {
  const selector = await screen.findByLabelText('Compañía principal')
  await screen.findByRole('option', { name: nombre })
  await userEvent.selectOptions(selector, id)
}

function renderizar(): void {
  const cliente = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  render(
    <QueryClientProvider client={cliente}>
      <UnidadesOrganizativasPage />
    </QueryClientProvider>,
  )
}

describe('UnidadesOrganizativasPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(
      paginaPrincipales([principal(P1, 'Minera Norte'), principal(P2, 'Minera Sur')]),
    )

    vi.spyOn(unidadesApi, 'listarUnidades').mockResolvedValue([])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('no consulta ningún árbol hasta elegir una compañía principal', async () => {
    const arbol = vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([])

    renderizar()

    // RF-043: los árboles están aislados por principal, así que no hay vista "de todas".
    expect(
      await screen.findByText('Elija una compañía principal para ver y administrar su jerarquía.'),
    ).toBeInTheDocument()

    expect(arbol).not.toHaveBeenCalled()
  })

  it('muestra la jerarquía de la principal seleccionada', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([
      nodo('r', 'Gerencia General', [nodo('h', 'Operaciones')]),
    ])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    expect(await screen.findByText('Gerencia General')).toBeInTheDocument()
  })

  it('consulta el árbol de cada principal por separado al cambiar de selección', async () => {
    const arbol = vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([nodo('r', 'Raíz')])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await screen.findByText('Raíz')

    await elegirPrincipal(P2, 'Minera Sur')

    // Cada principal se consulta con su propio identificador: la caché no puede mezclarlas.
    expect(arbol.mock.calls.map((c) => c[0])).toEqual([P1, P2])
  })

  it('avisa cuando la principal todavía no tiene unidades', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    expect(
      await screen.findByText(
        'Esta compañía principal todavía no tiene unidades. Cree la primera como raíz.',
      ),
    ).toBeInTheDocument()
  })

  it('crea un nodo raíz con la compañía principal seleccionada', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([])
    const crear = vi.spyOn(unidadesApi, 'crearUnidad').mockResolvedValue({
      id: 'r',
      nombre: 'Gerencia',
      unidadSuperiorId: null,
      companiaPrincipalId: P1,
      estado: 'ACTIVO',
    } satisfies UnidadOrganizativa)

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Gerencia')
    await userEvent.click(screen.getByRole('button', { name: 'Crear como raíz' }))

    // RF-045: un nodo raíz siempre nace asociado a una principal explícita.
    expect(crear.mock.calls[0][0]).toEqual({
      nombre: 'Gerencia',
      estado: 'ACTIVO',
      companiaPrincipalId: P1,
    })
  })

  it('no permite crear una unidad hija sin haber seleccionado una del árbol', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([nodo('r', 'Gerencia')])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Operaciones')

    expect(screen.getByRole('button', { name: 'Crear bajo la seleccionada' })).toBeDisabled()
    expect(
      screen.getByText('Seleccione una unidad del árbol para poder crear una unidad hija.'),
    ).toBeInTheDocument()
  })

  it('traduce el rechazo por ciclo a un mensaje comprensible', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([nodo('r', 'Gerencia')])
    vi.spyOn(unidadesApi, 'moverUnidad').mockRejectedValue(
      new ApiError({ status: 409, detail: 'La reubicación…', codigo: 'CICLO_JERARQUICO' }, 409),
    )

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.click(await screen.findByText('Gerencia'))
    await userEvent.click(screen.getByRole('button', { name: 'Mover' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se puede mover una unidad dentro de su propia rama.',
    )
  })

  it('traduce el rechazo por cruce entre principales', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([nodo('r', 'Gerencia')])
    vi.spyOn(unidadesApi, 'moverUnidad').mockRejectedValue(
      new ApiError(
        { status: 409, detail: 'No se puede…', codigo: 'COMPANIA_DEBE_SER_PRINCIPAL' },
        409,
      ),
    )

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.click(await screen.findByText('Gerencia'))
    await userEvent.click(screen.getByRole('button', { name: 'Mover' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se puede mover una unidad al árbol de otra compañía principal.',
    )
  })

  it('señala en el propio nombre las unidades inactivas', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([
      { id: 'r', nombre: 'Gerencia', estado: 'INACTIVO', hijos: [] },
    ])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    // El estado no puede depender solo del color (ux-ui.md §26).
    expect(await screen.findByText('Gerencia (inactiva)')).toBeInTheDocument()
  })

  it('la jerarquía se recorre con el ratón y la selección habilita las acciones (VF-002)', async () => {
    vi.spyOn(unidadesApi, 'obtenerArbol').mockResolvedValue([
      nodo('r', 'Gerencia General', [nodo('h', 'Operaciones')]),
    ])

    renderizar()
    await elegirPrincipal(P1, 'Minera Norte')

    const raiz = await screen.findByRole('treeitem', { name: /^Gerencia General/ })

    // RF-036: el árbol arranca contraído y la rama se despliega desde su indicador.
    expect(screen.queryByText('Operaciones')).toBeNull()
    await userEvent.click(within(raiz).getByText('▸'))
    expect(screen.getByText('Operaciones')).toBeInTheDocument()

    // Seleccionar el hijo sigue habilitando las acciones sobre la unidad seleccionada.
    await userEvent.click(screen.getByText('Operaciones'))
    expect(
      screen.getByText('La nueva unidad heredará la compañía principal de su unidad superior.'),
    ).toBeInTheDocument()
  })
})
