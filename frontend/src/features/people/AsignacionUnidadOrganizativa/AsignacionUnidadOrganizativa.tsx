import { useMemo, useState, type ReactElement } from 'react'
import { Tree, type NodoArbol } from '../../../components/Tree'
import { ApiError } from '../../../lib/apiClient'
import { formatearFechaHora } from '../../../lib/fechas'
import { useCompanias } from '../../companies/hooks'
import type { NodoArbolUnidad } from '../../org-units/api'
import { useArbolUnidades, useUnidades } from '../../org-units/hooks'
import { ETIQUETA_MOTIVO_REVOCACION, type ContextoOperativo } from '../history/api'
import { useAsignarUnidad, useUnidadesDelContexto } from '../history/hooks'

/** Adapta el árbol de la API al contrato de Tree, conservando la ruta de ancestros. */
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
 * Asignación de unidad organizativa dentro de un contexto operativo (RF-015, RF-055, RF-072).
 *
 * La Compañía Principal **no se elige aquí**: viene fijada por el contexto. Ofrecer un selector
 * permitiría intentar una unidad de otra Principal, que el servidor rechazaría (RF-055); limitar la
 * lista al árbol de la Principal del contexto evita proponer al usuario una opción inválida.
 */
export function AsignacionUnidadOrganizativa({
  personaId,
  contexto,
}: {
  personaId: string
  contexto: ContextoOperativo
}): ReactElement {
  const asignaciones = useUnidadesDelContexto(personaId, contexto.id)

  // Solo las unidades del árbol de la Principal de este contexto.
  const unidades = useUnidades(contexto.companiaPrincipalId)
  const arbol = useArbolUnidades(contexto.companiaPrincipalId)

  // La zona de la Principal del contexto rige la presentación de fechas (RF-080).
  const companias = useCompanias({ tamañoPagina: 200 })
  const zonaPrincipal =
    companias.data?.items.find((c) => c.id === contexto.companiaPrincipalId)?.zonaHorariaIana ??
    null

  const asignar = useAsignarUnidad(personaId)

  const nodos = useMemo(() => aNodosArbol(arbol.data ?? []), [arbol.data])

  const [unidadId, setUnidadId] = useState('')
  const [desde, setDesde] = useState(contexto.fechaHoraInicio.slice(0, 10))
  const [hasta, setHasta] = useState(contexto.fechaHoraFin.slice(0, 10))

  const error = asignar.error instanceof ApiError ? asignar.error : undefined

  const mensajeError =
    error?.codigo === 'FUERA_DE_CONTENCION_TEMPORAL'
      ? 'Las fechas deben quedar dentro de la vigencia de la pertenencia de la persona.'
      : error?.codigo === 'COMPANIA_DEBE_SER_PRINCIPAL'
        ? 'Esa unidad pertenece al árbol de otra compañía principal.'
        : error?.codigo === 'SOLAPAMIENTO_VIGENCIA'
          ? 'La nueva asignación debe comenzar después de la vigente en este contexto.'
          : error?.message

  return (
    <div className="asignacion-uo">
      <h3>Unidad organizativa en este contexto</h3>

      {error && (
        <p className="aviso error" role="alert">
          {mensajeError}
        </p>
      )}

      {asignaciones.isPending && <p>Cargando asignaciones…</p>}

      {!asignaciones.isPending && asignaciones.data?.length === 0 && (
        <p className="campo-ayuda">Este contexto todavía no tiene unidad organizativa asignada.</p>
      )}

      {(asignaciones.data?.length ?? 0) > 0 && (
        <ul className="asignacion-uo-lista">
          {asignaciones.data!.map((asignacion) => (
            <li key={asignacion.id}>
              {/* Nunca se muestra el UUID como respaldo (RF-013): mientras el nombre no ha llegado
                  se indica la carga, y si no resuelve se usa un marcador legible. */}
              <span>
                {unidades.isPending ? (
                  <span className="cargando">Cargando nombre…</span>
                ) : (
                  (unidades.data?.find((u) => u.id === asignacion.unidadOrganizativaId)?.nombre ?? (
                    <span className="sin-resolver">Unidad no disponible</span>
                  ))
                )}
              </span>

              <span className={`etiqueta ${asignacion.estado}`}>{asignacion.estado}</span>

              <span className="asignacion-uo-fechas">
                {formatearFechaHora(asignacion.fechaHoraInicio, zonaPrincipal)} –{' '}
                {formatearFechaHora(asignacion.fechaHoraFin, zonaPrincipal)}
              </span>

              {asignacion.motivoFin && (
                <span className="asignacion-uo-motivo">
                  {ETIQUETA_MOTIVO_REVOCACION[asignacion.motivoFin]}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {contexto.estado === 'ACTIVO' && (
        <div className="asignacion-uo-alta">
          {/* CS-021 exige elegir sobre el árbol, no sobre una lista plana: un selector lineal pierde
              la jerarquía que da sentido a los nombres repetidos entre ramas (ux-ui.md §14). */}
          <div className="campo">
            <span className="campo-etiqueta" id={`unidad-${contexto.id}-etiqueta`}>
              Unidad organizativa
            </span>

            {arbol.isPending && <p>Cargando el árbol de la compañía principal…</p>}

            {!arbol.isPending && nodos.length === 0 && (
              <p className="campo-ayuda">
                La compañía principal de este contexto todavía no tiene unidades organizativas.
              </p>
            )}

            {/* La etiqueta describe el alcance del árbol sin nombrar un selector de Principal: la
                Principal no se elige aquí, la fija el contexto (RF-055). */}
            {nodos.length > 0 && (
              <Tree
                nodos={nodos}
                etiqueta="Unidades organizativas disponibles en este contexto operativo"
                seleccionadoId={unidadId === '' ? undefined : unidadId}
                onSeleccionar={(nodo) => setUnidadId(nodo.id)}
              />
            )}

            <span className="campo-ayuda">
              Solo unidades de la compañía principal de este contexto.
            </span>
          </div>

          <div className="campo">
            <label htmlFor={`desde-${contexto.id}`}>Desde</label>
            <input
              id={`desde-${contexto.id}`}
              type="date"
              value={desde}
              onChange={(evento) => setDesde(evento.target.value)}
            />
          </div>

          <div className="campo">
            <label htmlFor={`hasta-${contexto.id}`}>Hasta</label>
            <input
              id={`hasta-${contexto.id}`}
              type="date"
              value={hasta}
              onChange={(evento) => setHasta(evento.target.value)}
            />
            <span className="campo-ayuda">
              Obligatorio: no existe asignación de vigencia indefinida.
            </span>
          </div>

          <button
            type="button"
            className="primario"
            disabled={unidadId === '' || desde === '' || hasta === '' || asignar.isPending}
            onClick={() =>
              asignar.mutate(
                {
                  contextoId: contexto.id,
                  unidadOrganizativaId: unidadId,
                  fechaHoraInicio: new Date(`${desde}T00:00:00Z`).toISOString(),
                  fechaHoraFin: new Date(`${hasta}T00:00:00Z`).toISOString(),
                },
                { onSuccess: () => setUnidadId('') },
              )
            }
          >
            {asignar.isPending ? 'Asignando…' : 'Asignar unidad'}
          </button>
        </div>
      )}
    </div>
  )
}
