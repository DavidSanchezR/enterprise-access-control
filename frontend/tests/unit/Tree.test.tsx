import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Tree, type NodoArbol } from '../../src/components/Tree'

const nodos: NodoArbol[] = [
  {
    id: 'operaciones',
    nombre: 'Operaciones',
    hijos: [
      {
        id: 'mina',
        nombre: 'Mina',
        hijos: [{ id: 'mantenimiento', nombre: 'Mantenimiento' }],
      },
    ],
  },
  { id: 'administracion', nombre: 'Administración' },
]

describe('Tree (patrón ARIA treeview, RF-036 / ux-ui.md §14, §26)', () => {
  it('expone roles y niveles jerárquicos accesibles', () => {
    render(<Tree nodos={nodos} etiqueta="Unidades organizativas de Minera ABC" />)

    expect(screen.getByRole('tree', { name: /Minera ABC/ })).toBeInTheDocument()

    const raices = screen.getAllByRole('treeitem')
    expect(raices).toHaveLength(2)
    expect(raices[0]).toHaveAttribute('aria-level', '1')
    expect(raices[0]).toHaveAttribute('aria-expanded', 'false')
  })

  it('permite expandir y contraer con el teclado', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={nodos} etiqueta="Unidades organizativas" />)

    const operaciones = screen.getByRole('treeitem', { name: /Operaciones/ })
    operaciones.focus()

    await usuario.keyboard('{ArrowRight}')
    expect(screen.getByRole('treeitem', { name: /Operaciones/ })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
    expect(screen.getByRole('treeitem', { name: /Mina/ })).toBeInTheDocument()

    await usuario.keyboard('{ArrowLeft}')
    expect(screen.getByRole('treeitem', { name: /Operaciones/ })).toHaveAttribute(
      'aria-expanded',
      'false',
    )
    expect(screen.queryByRole('treeitem', { name: /Mina/ })).not.toBeInTheDocument()
  })

  it('navega entre nodos visibles con las flechas arriba/abajo', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={nodos} etiqueta="Unidades organizativas" />)

    const operaciones = screen.getByRole('treeitem', { name: /Operaciones/ })
    operaciones.focus()

    await usuario.keyboard('{ArrowDown}')
    expect(screen.getByRole('treeitem', { name: /Administración/ })).toHaveFocus()

    await usuario.keyboard('{ArrowUp}')
    expect(screen.getByRole('treeitem', { name: /Operaciones/ })).toHaveFocus()
  })

  it('selecciona con Enter y anuncia el estado sin depender del color', async () => {
    const usuario = userEvent.setup()
    const alSeleccionar = vi.fn()

    render(
      <Tree
        nodos={nodos}
        etiqueta="Unidades organizativas"
        seleccionadoId="administracion"
        onSeleccionar={alSeleccionar}
      />,
    )

    const administracion = screen.getByRole('treeitem', { name: /Administración/ })

    // El estado seleccionado se comunica por aria-selected y por texto, no solo por color.
    expect(administracion).toHaveAttribute('aria-selected', 'true')
    expect(administracion).toHaveTextContent(/seleccionado/)

    administracion.focus()
    await usuario.keyboard('{Enter}')
    expect(alSeleccionar).toHaveBeenCalledWith(expect.objectContaining({ id: 'administracion' }))
  })

  it('usa roving tabindex: solo un nodo es alcanzable con Tab', () => {
    render(<Tree nodos={nodos} etiqueta="Unidades organizativas" />)

    const alcanzables = screen
      .getAllByRole('treeitem')
      .filter((nodo) => nodo.getAttribute('tabindex') === '0')

    expect(alcanzables).toHaveLength(1)
  })
})
