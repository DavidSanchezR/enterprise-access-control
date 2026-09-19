import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as areasApi from '../../src/features/area-access/api'
import type { AreaAcceso, NodoArbolArea } from '../../src/features/area-access/api'
import { AreasAccesoPage } from '../../src/features/area-access/AreasAccesoPage'
import * as companiasApi from '../../src/features/companies/api'
import type { Compania, PaginaCompanias } from '../../src/features/companies/api'

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

function nodo(id: string, nombre: string, hijos: NodoArbolArea[] = []): NodoArbolArea {
  return { id, nombre, estado: 'ACTIVO', hijos }
}

/**
 * Elige una compañía principal esperando primero a que el selector tenga sus opciones.
 *
 * Las principales llegan de una consulta asíncrona: sin la espera, `selectOptions` actuaría sobre un
 * `<select>` que todavía solo contiene el marcador de posición.
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
      <AreasAccesoPage />
    </QueryClientProvider>,
  )
}

describe('AreasAccesoPage', () => {
  beforeEach(() => {
    localStorage.clear()

    vi.spyOn(companiasApi, 'listarCompanias').mockResolvedValue(
      paginaPrincipales([principal(P1, 'Minera Norte'), principal(P2, 'Minera Sur')]),
    )

    vi.spyOn(areasApi, 'listarAreas').mockResolvedValue([])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('no consulta ningún árbol hasta elegir una compañía principal', async () => {
    const arbol = vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([])

    renderizar()

    // RF-043: los árboles de áreas están aislados por principal, así que no hay vista "de todas".
    expect(
      await screen.findByText(
        'Elija una compañía principal para ver y administrar sus áreas de acceso.',
      ),
    ).toBeInTheDocument()

    expect(arbol).not.toHaveBeenCalled()
  })

  it('muestra el árbol de áreas de la principal seleccionada', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([
      nodo('r', 'Planta Concentradora', [nodo('h', 'Molienda')]),
    ])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    expect(await screen.findByText('Planta Concentradora')).toBeInTheDocument()

    // El árbol nace colapsado: el área hija aparece al expandir su padre, no antes.
    expect(screen.queryByText('Molienda')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText('Planta Concentradora'))
    await userEvent.keyboard('{ArrowRight}')

    expect(await screen.findByText('Molienda')).toBeInTheDocument()
  })

  it('consulta el árbol de cada principal por separado al cambiar de selección', async () => {
    const arbol = vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([nodo('r', 'Planta')])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await screen.findByText('Planta')

    await elegirPrincipal(P2, 'Minera Sur')

    // Cada principal se consulta con su propio identificador: la caché no puede mezclarlas.
    expect(arbol.mock.calls.map((c) => c[0])).toEqual([P1, P2])
  })

  it('avisa cuando la principal todavía no tiene áreas', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    expect(
      await screen.findByText(
        'Esta compañía principal todavía no tiene áreas. Cree la primera como raíz.',
      ),
    ).toBeInTheDocument()
  })

  it('crea un área raíz con la compañía principal seleccionada', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([])
    const crear = vi.spyOn(areasApi, 'crearArea').mockResolvedValue({
      id: 'r',
      nombre: 'Planta',
      areaSuperiorId: null,
      companiaPrincipalId: P1,
      estado: 'ACTIVO',
    } satisfies AreaAcceso)

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Planta')
    await userEvent.click(screen.getByRole('button', { name: 'Crear como raíz' }))

    // RF-046: un área raíz siempre nace asociada a una principal explícita.
    expect(crear.mock.calls[0][0]).toEqual({
      nombre: 'Planta',
      estado: 'ACTIVO',
      companiaPrincipalId: P1,
    })
  })

  it('crea un área hija sin enviar compañía principal, porque la hereda del padre', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([nodo('r', 'Planta')])
    const crear = vi.spyOn(areasApi, 'crearArea').mockResolvedValue({
      id: 'h',
      nombre: 'Molienda',
      areaSuperiorId: 'r',
      companiaPrincipalId: P1,
      estado: 'ACTIVO',
    } satisfies AreaAcceso)

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.click(await screen.findByText('Planta'))
    await userEvent.type(screen.getByLabelText('Nombre'), 'Molienda')
    await userEvent.click(screen.getByRole('button', { name: 'Crear bajo la seleccionada' }))

    // El servidor ignora companiaPrincipalId en un área hija; enviarlo sugeriría que puede elegirse.
    expect(crear.mock.calls[0][0]).toEqual({
      nombre: 'Molienda',
      estado: 'ACTIVO',
      areaSuperiorId: 'r',
    })
  })

  it('no permite crear un área hija sin haber seleccionado una del árbol', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([nodo('r', 'Planta')])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.type(screen.getByLabelText('Nombre'), 'Molienda')

    expect(screen.getByRole('button', { name: 'Crear bajo la seleccionada' })).toBeDisabled()
    expect(
      screen.getByText('Seleccione un área del árbol para poder crear un área hija.'),
    ).toBeInTheDocument()
  })

  it('traduce el rechazo por ciclo a un mensaje comprensible', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([nodo('r', 'Planta')])
    vi.spyOn(areasApi, 'moverArea').mockRejectedValue(
      new ApiError({ status: 409, detail: 'La reubicación…', codigo: 'CICLO_JERARQUICO' }, 409),
    )

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.click(await screen.findByText('Planta'))
    await userEvent.click(screen.getByRole('button', { name: 'Mover' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se puede mover un área dentro de su propia rama.',
    )
  })

  it('traduce el rechazo por cruce entre principales', async () => {
    const { ApiError } = await import('../../src/lib/apiClient')

    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([nodo('r', 'Planta')])
    vi.spyOn(areasApi, 'moverArea').mockRejectedValue(
      new ApiError(
        { status: 409, detail: 'No se puede…', codigo: 'COMPANIA_DEBE_SER_PRINCIPAL' },
        409,
      ),
    )

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')
    await userEvent.click(await screen.findByText('Planta'))
    await userEvent.click(screen.getByRole('button', { name: 'Mover' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No se puede mover un área al árbol de otra compañía principal.',
    )
  })

  it('señala en el propio nombre las áreas inactivas', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([
      { id: 'r', nombre: 'Planta', estado: 'INACTIVO', hijos: [] },
    ])

    renderizar()

    await elegirPrincipal(P1, 'Minera Norte')

    // El estado no puede depender solo del color (ux-ui.md §26).
    expect(await screen.findByText('Planta (inactiva)')).toBeInTheDocument()
  })

  it('solo ofrece compañías principales en el selector', async () => {
    vi.spyOn(areasApi, 'obtenerArbolAreas').mockResolvedValue([])

    renderizar()

    await screen.findByRole('option', { name: 'Minera Norte' })

    // RF-046: una CONTRATISTA no posee áreas, así que no debe ni aparecer como opción.
    expect(vi.mocked(companiasApi.listarCompanias).mock.calls[0][0]).toMatchObject({
      tipoCompania: 'PRINCIPAL_MANDANTE',
    })
  })
})
