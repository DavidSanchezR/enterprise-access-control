import { useMemo, useState, type ReactElement } from 'react'
import { Tree, type NodoArbol } from '../../components/Tree'
import { ApiError } from '../../lib/apiClient'
import { useCompanias } from '../companies/hooks'
import type { NodoArbolUnidad } from './api'
import { useArbolUnidades, useCrearUnidad, useMoverUnidad, useUnidades } from './hooks'
import './orgUnits.css'

/** Adapta el árbol de la API al contrato del componente Tree, conservando la ruta de ancestros. */
function aNodosArbol(nodos: NodoArbolUnidad[], ruta: string[] = []): NodoArbol[] {
  return nodos.map((nodo) => ({
    id: nodo.id,
    // El estado viaja en el nombre visible para que no dependa solo del color (ux-ui.md §26).
    nombre: nodo.estado === 'INACTIVO' ? `${nodo.nombre} (inactiva)` : nodo.nombre,
    rutaAncestros: ruta,
    hijos: aNodosArbol(nodo.hijos, [...ruta, nodo.nombre]),
  }))
}

/**
 * Árbol de unidades organizativas de una Compañía Principal (Historia 2, RF-007, RF-008, RF-043).
 *
 * La Principal se elige primero y de forma explícita: cada una tiene su árbol aislado, así que no
 * existe una vista "de todas las unidades" que sea correcta mostrar.
 */
export function UnidadesOrganizativasPage(): ReactElement {
  const [companiaPrincipalId, setCompaniaPrincipalId] = useState('')
  const [seleccionadoId, setSeleccionadoId] = useState<string | undefined>()
  const [nombreNuevo, setNombreNuevo] = useState('')
  const [destinoId, setDestinoId] = useState('')

  const principales = useCompanias({
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    tamañoPagina: 200,
  })

  const arbol = useArbolUnidades(companiaPrincipalId === '' ? null : companiaPrincipalId)
  const planas = useUnidades(companiaPrincipalId === '' ? null : companiaPrincipalId)

  const crear = useCrearUnidad()
  const mover = useMoverUnidad()

  const nodos = useMemo(() => aNodosArbol(arbol.data ?? []), [arbol.data])

  const error =
    (arbol.error instanceof ApiError && arbol.error) ||
    (crear.error instanceof ApiError && crear.error) ||
    (mover.error instanceof ApiError && mover.error) ||
    undefined

  const haySeleccion = seleccionadoId !== undefined
  const arbolVacio = (arbol.data ?? []).length === 0

  function crearNodo(comoRaiz: boolean): void {
    if (nombreNuevo.trim() === '') {
      return
    }

    crear.mutate(
      comoRaiz
        ? { nombre: nombreNuevo.trim(), estado: 'ACTIVO', companiaPrincipalId }
        : { nombre: nombreNuevo.trim(), estado: 'ACTIVO', unidadSuperiorId: seleccionadoId },
      { onSuccess: () => setNombreNuevo('') },
    )
  }

  return (
    <section className="unidades">
      <header>
        <h1>Unidades organizativas</h1>
        <p className="unidades-subtitulo">
          Cada compañía principal tiene su propia jerarquía, independiente de las demás.
        </p>
      </header>

      <div className="campo unidades-selector">
        <label htmlFor="principal">Compañía principal</label>
        <select
          id="principal"
          value={companiaPrincipalId}
          onChange={(evento) => {
            setCompaniaPrincipalId(evento.target.value)
            setSeleccionadoId(undefined)
          }}
        >
          <option value="">Seleccione una compañía principal…</option>
          {principales.data?.items.map((principal) => (
            <option key={principal.id} value={principal.id}>
              {principal.nombre}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.codigo === 'CICLO_JERARQUICO'
            ? 'No se puede mover una unidad dentro de su propia rama.'
            : error.codigo === 'COMPANIA_DEBE_SER_PRINCIPAL'
              ? 'No se puede mover una unidad al árbol de otra compañía principal.'
              : error.message}
        </p>
      )}

      {companiaPrincipalId === '' && (
        <p className="unidades-vacio">
          Elija una compañía principal para ver y administrar su jerarquía.
        </p>
      )}

      {companiaPrincipalId !== '' && (
        <div className="unidades-panel">
          <div className="tarjeta unidades-arbol">
            {arbol.isPending && <p>Cargando jerarquía…</p>}

            {!arbol.isPending && arbolVacio && (
              <p className="unidades-vacio">
                Esta compañía principal todavía no tiene unidades. Cree la primera como raíz.
              </p>
            )}

            {!arbolVacio && (
              <Tree
                nodos={nodos}
                etiqueta={`Unidades organizativas de ${
                  principales.data?.items.find((c) => c.id === companiaPrincipalId)?.nombre ??
                  'la compañía seleccionada'
                }`}
                seleccionadoId={seleccionadoId}
                onSeleccionar={(nodo) => setSeleccionadoId(nodo.id)}
              />
            )}
          </div>

          <div className="tarjeta unidades-acciones">
            <h2>Agregar unidad</h2>

            <div className="campo">
              <label htmlFor="nombre-nuevo">Nombre</label>
              <input
                id="nombre-nuevo"
                type="text"
                value={nombreNuevo}
                onChange={(evento) => setNombreNuevo(evento.target.value)}
              />
            </div>

            <div className="unidades-botones">
              <button
                type="button"
                className="primario"
                disabled={nombreNuevo.trim() === '' || crear.isPending}
                onClick={() => crearNodo(true)}
              >
                Crear como raíz
              </button>

              <button
                type="button"
                disabled={!haySeleccion || nombreNuevo.trim() === '' || crear.isPending}
                onClick={() => crearNodo(false)}
              >
                Crear bajo la seleccionada
              </button>
            </div>

            <p className="campo-ayuda">
              {haySeleccion
                ? 'La nueva unidad heredará la compañía principal de su unidad superior.'
                : 'Seleccione una unidad del árbol para poder crear una unidad hija.'}
            </p>

            <h2 className="unidades-mover">Reubicar la unidad seleccionada</h2>

            <div className="campo">
              <label htmlFor="destino">Nueva unidad superior</label>
              <select
                id="destino"
                value={destinoId}
                disabled={!haySeleccion}
                onChange={(evento) => setDestinoId(evento.target.value)}
              >
                <option value="">Convertir en raíz</option>
                {(planas.data ?? [])
                  .filter((unidad) => unidad.id !== seleccionadoId)
                  .map((unidad) => (
                    <option key={unidad.id} value={unidad.id}>
                      {unidad.nombre}
                    </option>
                  ))}
              </select>
            </div>

            <button
              type="button"
              disabled={!haySeleccion || mover.isPending}
              onClick={() =>
                mover.mutate({
                  id: seleccionadoId!,
                  nuevoPadreId: destinoId === '' ? null : destinoId,
                })
              }
            >
              {mover.isPending ? 'Moviendo…' : 'Mover'}
            </button>

            <p className="campo-ayuda">
              Solo se admiten destinos dentro de esta misma compañía principal, y nunca dentro de la
              propia rama de la unidad.
            </p>
          </div>
        </div>
      )}
    </section>
  )
}
