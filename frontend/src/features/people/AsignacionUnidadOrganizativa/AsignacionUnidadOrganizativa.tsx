import { useState, type ReactElement } from 'react'
import { ApiError } from '../../../lib/apiClient'
import { useUnidades } from '../../org-units/hooks'
import { ETIQUETA_MOTIVO_REVOCACION, type ContextoOperativo } from '../history/api'
import { useAsignarUnidad, useUnidadesDelContexto } from '../history/hooks'

function formatearFecha(iso: string): string {
  return new Date(iso).toLocaleDateString()
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

  const asignar = useAsignarUnidad(personaId)

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
              <span>
                {unidades.data?.find((u) => u.id === asignacion.unidadOrganizativaId)?.nombre ??
                  asignacion.unidadOrganizativaId}
              </span>

              <span className={`etiqueta ${asignacion.estado}`}>{asignacion.estado}</span>

              <span className="asignacion-uo-fechas">
                {formatearFecha(asignacion.fechaHoraInicio)} –{' '}
                {formatearFecha(asignacion.fechaHoraFin)}
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
          <div className="campo">
            <label htmlFor={`unidad-${contexto.id}`}>Unidad organizativa</label>
            <select
              id={`unidad-${contexto.id}`}
              value={unidadId}
              onChange={(evento) => setUnidadId(evento.target.value)}
            >
              <option value="">Seleccione…</option>
              {unidades.data?.map((unidad) => (
                <option key={unidad.id} value={unidad.id}>
                  {unidad.nombre}
                </option>
              ))}
            </select>
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
