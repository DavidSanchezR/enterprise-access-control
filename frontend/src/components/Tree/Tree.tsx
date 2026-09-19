import {
  useCallback,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type ReactElement,
} from 'react'

export interface NodoArbol {
  id: string
  nombre: string
  hijos?: NodoArbol[]
  /** Ruta de ancestros, usada para desambiguar nombres repetidos (ux-ui.md §14). */
  rutaAncestros?: string[]
}

export interface TreeProps {
  nodos: NodoArbol[]
  /** Etiqueta accesible del árbol; incluye el contexto de Principal (ux-ui.md §14). */
  etiqueta: string
  seleccionadoId?: string
  onSeleccionar?: (nodo: NodoArbol) => void
}

interface NodoVisible {
  nodo: NodoArbol
  nivel: number
  tieneHijos: boolean
  expandido: boolean
}

/**
 * Árbol accesible según el patrón ARIA `treeview` (RF-036, ux-ui.md §14, §26; WCAG 2.2 AA).
 *
 * Decisiones de accesibilidad relevantes:
 * - Un único `tabIndex=0` recorre el árbol (roving tabindex): el usuario de teclado entra y sale
 *   del componente con un solo Tab en lugar de atravesar cientos de nodos.
 * - El estado expandido/colapsado viaja en `aria-expanded` y el nivel en `aria-level`, de modo que
 *   un lector de pantalla anuncia la posición jerárquica real.
 * - La selección no depende solo del color: el nodo seleccionado se marca con `aria-selected` y un
 *   indicador textual (ux-ui.md §26: "estados no dependientes solo del color").
 */
export function Tree({ nodos, etiqueta, seleccionadoId, onSeleccionar }: TreeProps): ReactElement {
  const treeId = useId()
  const [expandidos, setExpandidos] = useState<ReadonlySet<string>>(() => new Set())
  const [enfocadoId, setEnfocadoId] = useState<string | undefined>(nodos[0]?.id)
  const contenedorRef = useRef<HTMLUListElement>(null)

  // Aplanar respetando el estado de expansión: la navegación por teclado opera sobre lo que el
  // usuario ve, no sobre el árbol completo.
  const visibles = useMemo(() => {
    const salida: NodoVisible[] = []

    const recorrer = (lista: NodoArbol[], nivel: number): void => {
      for (const nodo of lista) {
        const tieneHijos = (nodo.hijos?.length ?? 0) > 0
        const expandido = expandidos.has(nodo.id)
        salida.push({ nodo, nivel, tieneHijos, expandido })
        if (tieneHijos && expandido) {
          recorrer(nodo.hijos!, nivel + 1)
        }
      }
    }

    recorrer(nodos, 1)
    return salida
  }, [nodos, expandidos])

  const alternar = useCallback((id: string) => {
    setExpandidos((previo) => {
      const siguiente = new Set(previo)
      if (siguiente.has(id)) {
        siguiente.delete(id)
      } else {
        siguiente.add(id)
      }
      return siguiente
    })
  }, [])

  const enfocar = useCallback((id: string) => {
    setEnfocadoId(id)
    contenedorRef.current?.querySelector<HTMLElement>(`[data-nodo-id="${CSS.escape(id)}"]`)?.focus()
  }, [])

  const manejarTeclado = useCallback(
    (evento: KeyboardEvent<HTMLLIElement>, item: NodoVisible) => {
      const indice = visibles.findIndex((v) => v.nodo.id === item.nodo.id)

      switch (evento.key) {
        case 'ArrowDown':
          evento.preventDefault()
          if (indice < visibles.length - 1) {
            enfocar(visibles[indice + 1].nodo.id)
          }
          break

        case 'ArrowUp':
          evento.preventDefault()
          if (indice > 0) {
            enfocar(visibles[indice - 1].nodo.id)
          }
          break

        case 'ArrowRight':
          evento.preventDefault()
          // Expande; si ya estaba expandido, baja al primer hijo (patrón ARIA treeview).
          if (item.tieneHijos && !item.expandido) {
            alternar(item.nodo.id)
          } else if (item.tieneHijos && indice < visibles.length - 1) {
            enfocar(visibles[indice + 1].nodo.id)
          }
          break

        case 'ArrowLeft': {
          evento.preventDefault()
          if (item.tieneHijos && item.expandido) {
            alternar(item.nodo.id)
          } else {
            // Sube al padre: el ancestro visible más cercano con nivel menor.
            for (let i = indice - 1; i >= 0; i--) {
              if (visibles[i].nivel < item.nivel) {
                enfocar(visibles[i].nodo.id)
                break
              }
            }
          }
          break
        }

        case 'Home':
          evento.preventDefault()
          if (visibles.length > 0) {
            enfocar(visibles[0].nodo.id)
          }
          break

        case 'End':
          evento.preventDefault()
          if (visibles.length > 0) {
            enfocar(visibles[visibles.length - 1].nodo.id)
          }
          break

        case 'Enter':
        case ' ':
          evento.preventDefault()
          onSeleccionar?.(item.nodo)
          break

        default:
          break
      }
    },
    [visibles, enfocar, alternar, onSeleccionar],
  )

  // Punto de tabulación efectivo: el nodo recordado si sigue visible; si no (se contrajo su padre o
  // los datos se recargaron sin él), el primero visible. Sin este respaldo ningún nodo tendría
  // tabIndex=0 y el árbol quedaría inalcanzable por teclado (WCAG 2.1.1).
  const tabulableId = visibles.some((v) => v.nodo.id === enfocadoId)
    ? enfocadoId
    : visibles[0]?.nodo.id

  if (nodos.length === 0) {
    return (
      <p role="status" aria-live="polite">
        No hay nodos para mostrar en este árbol.
      </p>
    )
  }

  return (
    <ul
      ref={contenedorRef}
      role="tree"
      aria-label={etiqueta}
      id={treeId}
      style={{ listStyle: 'none', paddingLeft: 0, margin: 0 }}
    >
      {visibles.map((item) => {
        const seleccionado = item.nodo.id === seleccionadoId
        const esEnfocado = item.nodo.id === tabulableId

        return (
          <li
            key={item.nodo.id}
            role="treeitem"
            data-nodo-id={item.nodo.id}
            aria-level={item.nivel}
            aria-expanded={item.tieneHijos ? item.expandido : undefined}
            aria-selected={seleccionado}
            // Roving tabindex: solo un nodo es alcanzable con Tab.
            tabIndex={esEnfocado ? 0 : -1}
            onKeyDown={(evento) => manejarTeclado(evento, item)}
            onFocus={() => setEnfocadoId(item.nodo.id)}
            onClick={(evento) => {
              evento.stopPropagation()
              onSeleccionar?.(item.nodo)
            }}
            style={{
              paddingLeft: `${item.nivel * 1.25}rem`,
              cursor: 'pointer',
              outlineOffset: '2px',
            }}
          >
            <span aria-hidden="true">
              {item.tieneHijos ? (item.expandido ? '▾ ' : '▸ ') : '• '}
            </span>
            <span>{item.nodo.nombre}</span>

            {/* El estado seleccionado se anuncia con texto, no solo con color (ux-ui.md §26). */}
            {seleccionado && <span className="sr-only"> (seleccionado)</span>}

            {/* Desambiguación de nombres iguales en árboles distintos (ux-ui.md §14). */}
            {item.nodo.rutaAncestros && item.nodo.rutaAncestros.length > 0 && (
              <span className="sr-only"> en {item.nodo.rutaAncestros.join(' / ')}</span>
            )}
          </li>
        )
      })}
    </ul>
  )
}
