import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { Tree, type NodoArbol } from '../../src/components/Tree'

/**
 * Auditoría de accesibilidad del componente Tree: navegación completa por teclado según el patrón
 * WAI-ARIA APG "Tree View" (RF-036, ux-ui.md §14 y §26, WCAG 2.2 AA — 2.1.1 Teclado, 2.4.3 Orden del
 * foco, 4.1.2 Nombre, función y valor).
 *
 * Complementa Tree.test.tsx, que cubre lo básico: aquí se recorre cada tecla del patrón y los casos
 * límite en los que un árbol accesible suele dejar de serlo (el foco se pierde, deja de haber un nodo
 * alcanzable con Tab, o una hoja anuncia un estado de expansión que no tiene).
 */

const arbol: NodoArbol[] = [
  {
    id: 'planta',
    nombre: 'Planta',
    hijos: [
      {
        id: 'molienda',
        nombre: 'Molienda',
        rutaAncestros: ['Planta'],
        hijos: [{ id: 'sala', nombre: 'Sala de control', rutaAncestros: ['Planta', 'Molienda'] }],
      },
      { id: 'chancado', nombre: 'Chancado', rutaAncestros: ['Planta'] },
    ],
  },
  { id: 'mina', nombre: 'Mina' },
  { id: 'oficinas', nombre: 'Oficinas' },
]

function nodo(nombre: RegExp): HTMLElement {
  return screen.getByRole('treeitem', { name: nombre })
}

function alcanzablesConTab(): HTMLElement[] {
  return screen.getAllByRole('treeitem').filter((n) => n.getAttribute('tabindex') === '0')
}

describe('Tree — auditoría de accesibilidad por teclado', () => {
  it('es un único punto de tabulación: Tab entra al árbol y el siguiente Tab sale de él', async () => {
    const usuario = userEvent.setup()

    render(
      <>
        <button type="button">Antes</button>
        <Tree nodos={arbol} etiqueta="Áreas de Minera Norte" />
        <button type="button">Después</button>
      </>,
    )

    await usuario.tab()
    expect(screen.getByRole('button', { name: 'Antes' })).toHaveFocus()

    await usuario.tab()
    expect(nodo(/^Planta/)).toHaveFocus()

    // WCAG 2.4.3: atravesar el árbol no exige recorrer cada nodo con Tab.
    await usuario.tab()
    expect(screen.getByRole('button', { name: 'Después' })).toHaveFocus()

    await usuario.tab({ shift: true })
    expect(nodo(/^Planta/)).toHaveFocus()
  })

  it('la flecha derecha expande un nodo cerrado y, si ya está abierto, baja a su primer hijo', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Planta/).focus()

    await usuario.keyboard('{ArrowRight}')
    expect(nodo(/^Planta/)).toHaveAttribute('aria-expanded', 'true')
    expect(nodo(/^Planta/)).toHaveFocus()

    await usuario.keyboard('{ArrowRight}')
    expect(nodo(/^Molienda/)).toHaveFocus()
  })

  it('la flecha derecha sobre una hoja no hace nada', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Mina/).focus()
    await usuario.keyboard('{ArrowRight}')

    expect(nodo(/^Mina/)).toHaveFocus()
    expect(screen.getAllByRole('treeitem')).toHaveLength(3)
  })

  it('la flecha izquierda contrae un nodo abierto y, desde un hijo, sube a su padre', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Planta/).focus()
    await usuario.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}{ArrowRight}')
    expect(nodo(/^Sala de control/)).toHaveFocus()

    await usuario.keyboard('{ArrowLeft}')
    expect(nodo(/^Molienda/)).toHaveFocus()

    await usuario.keyboard('{ArrowLeft}')
    expect(nodo(/^Molienda/)).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('treeitem', { name: /^Sala de control/ })).not.toBeInTheDocument()

    await usuario.keyboard('{ArrowLeft}')
    expect(nodo(/^Planta/)).toHaveFocus()
  })

  it('la flecha izquierda sobre una raíz cerrada no mueve el foco fuera del árbol', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Mina/).focus()
    await usuario.keyboard('{ArrowLeft}')

    expect(nodo(/^Mina/)).toHaveFocus()
  })

  it('las flechas arriba y abajo recorren solo los nodos visibles y se detienen en los extremos', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Planta/).focus()
    await usuario.keyboard('{ArrowRight}')

    const orden = ['Molienda', 'Chancado', 'Mina', 'Oficinas']
    for (const nombre of orden) {
      await usuario.keyboard('{ArrowDown}')
      expect(nodo(new RegExp(`^${nombre}`))).toHaveFocus()
    }

    // En el último nodo, abajo no sale del árbol ni vuelve al principio.
    await usuario.keyboard('{ArrowDown}')
    expect(nodo(/^Oficinas/)).toHaveFocus()

    await usuario.keyboard('{ArrowUp}{ArrowUp}{ArrowUp}{ArrowUp}{ArrowUp}')
    expect(nodo(/^Planta/)).toHaveFocus()
  })

  it('Inicio y Fin llevan al primer y al último nodo visible', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Planta/).focus()
    await usuario.keyboard('{ArrowRight}{End}')
    expect(nodo(/^Oficinas/)).toHaveFocus()

    await usuario.keyboard('{Home}')
    expect(nodo(/^Planta/)).toHaveFocus()
  })

  it('Enter y Espacio seleccionan el nodo enfocado', async () => {
    const usuario = userEvent.setup()
    const alSeleccionar = vi.fn()
    render(<Tree nodos={arbol} etiqueta="Áreas" onSeleccionar={alSeleccionar} />)

    nodo(/^Mina/).focus()
    await usuario.keyboard('{Enter}')
    expect(alSeleccionar).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'mina' }))

    nodo(/^Oficinas/).focus()
    await usuario.keyboard(' ')
    expect(alSeleccionar).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'oficinas' }))
  })

  it('el punto de tabulación acompaña al foco mientras se navega', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Planta/).focus()
    await usuario.keyboard('{ArrowDown}{ArrowDown}')

    // Salir y volver a entrar con Tab devuelve al último nodo visitado, no al primero.
    expect(alcanzablesConTab()).toEqual([nodo(/^Oficinas/)])
  })

  it('siempre queda exactamente un nodo alcanzable con Tab, aunque desaparezca el que tenía el foco', async () => {
    const usuario = userEvent.setup()
    const { rerender } = render(<Tree nodos={arbol} etiqueta="Áreas" />)

    nodo(/^Oficinas/).focus()
    await usuario.keyboard('{ArrowUp}')
    expect(nodo(/^Mina/)).toHaveFocus()

    // Los datos se recargan sin el nodo enfocado (p. ej. se reubicó en otro árbol).
    rerender(<Tree nodos={arbol.filter((n) => n.id !== 'mina')} etiqueta="Áreas" />)

    // Sin un punto de tabulación el árbol entero sería inalcanzable por teclado (WCAG 2.1.1).
    expect(alcanzablesConTab()).toHaveLength(1)
  })

  it('las hojas no anuncian un estado de expansión que no tienen', () => {
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    expect(nodo(/^Planta/)).toHaveAttribute('aria-expanded', 'false')
    expect(nodo(/^Mina/)).not.toHaveAttribute('aria-expanded')
  })

  it('cada nodo expone nivel, estado de selección y su ruta de ancestros para desambiguar', async () => {
    const usuario = userEvent.setup()
    render(<Tree nodos={arbol} etiqueta="Áreas" seleccionadoId="molienda" />)

    nodo(/^Planta/).focus()
    await usuario.keyboard('{ArrowRight}')

    const molienda = nodo(/^Molienda/)
    expect(molienda).toHaveAttribute('aria-level', '2')
    expect(molienda).toHaveAttribute('aria-selected', 'true')
    expect(nodo(/^Chancado/)).toHaveAttribute('aria-selected', 'false')

    // ux-ui.md §14: nombres repetidos en árboles distintos se distinguen por su ruta.
    expect(molienda).toHaveAccessibleName(/en Planta/)
  })

  it('los indicadores gráficos de expansión no forman parte del nombre accesible', () => {
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    expect(nodo(/^Planta/)).not.toHaveAccessibleName(/[▸▾•]/)
  })

  it('las teclas de navegación no desplazan la página', () => {
    render(<Tree nodos={arbol} etiqueta="Áreas" />)

    const planta = nodo(/^Planta/)
    planta.focus()

    for (const key of ['ArrowDown', 'ArrowUp', 'Home', 'End', ' ']) {
      const evento = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true })
      planta.dispatchEvent(evento)
      expect(evento.defaultPrevented, key).toBe(true)
    }
  })

  it('un árbol vacío lo comunica como estado en lugar de dejar un contenedor mudo', () => {
    render(<Tree nodos={[]} etiqueta="Áreas" />)

    expect(screen.getByRole('status')).toHaveTextContent('No hay nodos para mostrar')
  })
})
