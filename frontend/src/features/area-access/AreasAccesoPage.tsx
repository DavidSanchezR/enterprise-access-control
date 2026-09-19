import { useMemo, useState, type ReactElement } from 'react'
import { Tree, type NodoArbol } from '../../components/Tree'
import { ApiError } from '../../lib/apiClient'
import { useCompanias } from '../companies/hooks'
import type { NodoArbolArea } from './api'
import { useAreas, useArbolAreas, useCrearArea, useMoverArea } from './hooks'
import { TiposPersonaPorArea } from './TiposPersonaPorArea/TiposPersonaPorArea'
import './areaAccess.css'

/** Adapta el árbol de la API al contrato del componente Tree, conservando la ruta de ancestros. */
function aNodosArbol(nodos: NodoArbolArea[], ruta: string[] = []): NodoArbol[] {
  return nodos.map((nodo) => ({
    id: nodo.id,
    // El estado viaja en el nombre visible para que no dependa solo del color (ux-ui.md §26).
    nombre: nodo.estado === 'INACTIVO' ? `${nodo.nombre} (inactiva)` : nodo.nombre,
    rutaAncestros: ruta,
    hijos: aNodosArbol(nodo.hijos, [...ruta, nodo.nombre]),
  }))
}

/**
 * Árbol de áreas físicas de acceso de una Compañía Principal (Historia 6, RF-009, RF-038, RF-046).
 *
 * La Principal se elige primero y de forma explícita: cada una tiene su árbol aislado (RF-043), así
 * que no existe una vista "de todas las áreas" que sea correcta mostrar.
 *
 * La pantalla reproduce a propósito la de unidades organizativas: para quien administra son la misma
 * tarea —dibujar una jerarquía— y divergir en la interacción sólo obligaría a aprenderla dos veces.
 * Lo que sí cambia es el texto de ayuda, porque aquí la compañía propietaria es un dato del área.
 */
export function AreasAccesoPage(): ReactElement {
  const [companiaPrincipalId, setCompaniaPrincipalId] = useState('')
  const [seleccionadoId, setSeleccionadoId] = useState<string | undefined>()
  const [nombreNuevo, setNombreNuevo] = useState('')
  const [destinoId, setDestinoId] = useState('')

  const principales = useCompanias({
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    tamañoPagina: 200,
  })

  const arbol = useArbolAreas(companiaPrincipalId === '' ? null : companiaPrincipalId)
  const planas = useAreas(companiaPrincipalId === '' ? null : companiaPrincipalId)

  const crear = useCrearArea()
  const mover = useMoverArea()

  const nodos = useMemo(() => aNodosArbol(arbol.data ?? []), [arbol.data])

  const error =
    (arbol.error instanceof ApiError && arbol.error) ||
    (crear.error instanceof ApiError && crear.error) ||
    (mover.error instanceof ApiError && mover.error) ||
    undefined

  const haySeleccion = seleccionadoId !== undefined
  const arbolVacio = (arbol.data ?? []).length === 0
  const areaSeleccionada = (planas.data ?? []).find((area) => area.id === seleccionadoId)

  function crearNodo(comoRaiz: boolean): void {
    if (nombreNuevo.trim() === '') {
      return
    }

    crear.mutate(
      comoRaiz
        ? { nombre: nombreNuevo.trim(), estado: 'ACTIVO', companiaPrincipalId }
        : { nombre: nombreNuevo.trim(), estado: 'ACTIVO', areaSuperiorId: seleccionadoId },
      { onSuccess: () => setNombreNuevo('') },
    )
  }

  return (
    <section className="areas">
      <header>
        <h1>Áreas de acceso</h1>
        <p className="areas-subtitulo">
          Cada compañía principal tiene su propio árbol de áreas físicas, independiente de las
          demás.
        </p>
      </header>

      <div className="campo areas-selector">
        <label htmlFor="principal-areas">Compañía principal</label>
        <select
          id="principal-areas"
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
            ? 'No se puede mover un área dentro de su propia rama.'
            : error.codigo === 'COMPANIA_DEBE_SER_PRINCIPAL'
              ? 'No se puede mover un área al árbol de otra compañía principal.'
              : error.message}
        </p>
      )}

      {companiaPrincipalId === '' && (
        <p className="areas-vacio">
          Elija una compañía principal para ver y administrar sus áreas de acceso.
        </p>
      )}

      {companiaPrincipalId !== '' && (
        <div className="areas-panel">
          <div className="tarjeta areas-arbol">
            {arbol.isPending && <p>Cargando áreas…</p>}

            {!arbol.isPending && arbolVacio && (
              <p className="areas-vacio">
                Esta compañía principal todavía no tiene áreas. Cree la primera como raíz.
              </p>
            )}

            {!arbolVacio && (
              <Tree
                nodos={nodos}
                etiqueta={`Áreas de acceso de ${
                  principales.data?.items.find((c) => c.id === companiaPrincipalId)?.nombre ??
                  'la compañía seleccionada'
                }`}
                seleccionadoId={seleccionadoId}
                onSeleccionar={(nodo) => setSeleccionadoId(nodo.id)}
              />
            )}
          </div>

          <div className="tarjeta areas-acciones">
            <h2>Agregar área</h2>

            <div className="campo">
              <label htmlFor="nombre-area-nueva">Nombre</label>
              <input
                id="nombre-area-nueva"
                type="text"
                value={nombreNuevo}
                onChange={(evento) => setNombreNuevo(evento.target.value)}
              />
            </div>

            <div className="areas-botones">
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
                ? 'La nueva área pertenecerá a la misma compañía principal que su área superior.'
                : 'Seleccione un área del árbol para poder crear un área hija.'}
            </p>

            <h2 className="areas-mover">Reubicar el área seleccionada</h2>

            <div className="campo">
              <label htmlFor="destino-area">Nueva área superior</label>
              <select
                id="destino-area"
                value={destinoId}
                disabled={!haySeleccion}
                onChange={(evento) => setDestinoId(evento.target.value)}
              >
                <option value="">Convertir en raíz</option>
                {(planas.data ?? [])
                  .filter((area) => area.id !== seleccionadoId)
                  .map((area) => (
                    <option key={area.id} value={area.id}>
                      {area.nombre}
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
              El área conserva siempre su compañía principal: solo se admiten destinos de este mismo
              árbol, y nunca dentro de la propia rama del área.
            </p>
          </div>

          {/*
            Los tipos de persona se declaran por área (RF-019), así que el panel depende de la
            selección del árbol y no de la Compañía Principal.
          */}
          {areaSeleccionada ? (
            <TiposPersonaPorArea
              key={areaSeleccionada.id}
              areaId={areaSeleccionada.id}
              nombreArea={areaSeleccionada.nombre}
            />
          ) : (
            !arbolVacio && (
              <p className="areas-vacio areas-tipos-aviso">
                Seleccione un área del árbol para ver y editar los tipos de persona autorizados en
                ella.
              </p>
            )
          )}
        </div>
      )}
    </section>
  )
}
