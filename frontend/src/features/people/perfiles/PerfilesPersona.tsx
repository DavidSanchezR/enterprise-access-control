import { useState, type ReactElement } from 'react'
import { ApiError } from '../../../lib/apiClient'
import { formatearFechaHora } from '../../../lib/fechas'
import { useMaestro } from '../../masters/hooks'
import { useAsignarPerfil, usePerfiles } from './hooks'

/**
 * Perfiles vigentes e históricos de una persona (RF-011, `AsignaciónTipoPersona`).
 *
 * Un perfil no está ligado a ninguna compañía, así que su vigencia no es resoluble a una única
 * Compañía Principal: las fechas se presentan en la zona horaria global de respaldo (RF-080).
 */
export function PerfilesPersona({ personaId }: { personaId: string }): ReactElement {
  const perfiles = usePerfiles(personaId)
  const tipos = useMaestro('tipos-persona', 'ACTIVO')
  const asignar = useAsignarPerfil(personaId)

  const [tipoPersonaId, setTipoPersonaId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')

  const error = asignar.error instanceof ApiError ? asignar.error : undefined

  const nombreTipo = (id: string): ReactElement | string => {
    if (tipos.isPending) {
      return <span className="cargando">Cargando nombre…</span>
    }

    // Nunca se muestra el UUID como respaldo (RF-013).
    return (
      tipos.data?.find((t) => t.id === id)?.nombre ?? (
        <span className="sin-resolver">Perfil no disponible</span>
      )
    )
  }

  return (
    <section className="perfiles-persona">
      <h2 className="historial-seccion">Perfiles</h2>

      <p className="historial-ayuda">
        El perfil determina en qué áreas puede autorizarse su acceso. Una persona puede tener varios
        a lo largo del tiempo, sin solaparse.
      </p>

      {error && (
        <p className="aviso error" role="alert">
          {error.codigo === 'SOLAPAMIENTO_VIGENCIA'
            ? 'Ya existe un perfil vigente para ese intervalo; ciérrelo antes o ajuste las fechas.'
            : error.message}
        </p>
      )}

      {perfiles.isPending && <p>Cargando perfiles…</p>}

      {!perfiles.isPending && perfiles.data?.length === 0 && (
        <p className="historial-vacio">Esta persona todavía no tiene ningún perfil asignado.</p>
      )}

      {(perfiles.data?.length ?? 0) > 0 && (
        <ul className="perfiles-lista">
          {perfiles.data!.map((perfil) => (
            <li key={perfil.id}>
              <span>{nombreTipo(perfil.tipoPersonaId)}</span>
              <span className={`etiqueta ${perfil.estado}`}>{perfil.estado}</span>
              <span className="asignacion-uo-fechas">
                {formatearFechaHora(perfil.fechaHoraInicio)} –{' '}
                {formatearFechaHora(perfil.fechaHoraFin)}
              </span>
            </li>
          ))}
        </ul>
      )}

      <div className="asignacion-uo-alta">
        <div className="campo">
          <label htmlFor="perfil-tipo">Perfil</label>
          <select
            id="perfil-tipo"
            value={tipoPersonaId}
            onChange={(evento) => setTipoPersonaId(evento.target.value)}
          >
            <option value="">Seleccione…</option>
            {tipos.data?.map((tipo) => (
              <option key={tipo.id} value={tipo.id}>
                {tipo.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="perfil-desde">Desde</label>
          <input
            id="perfil-desde"
            type="date"
            value={desde}
            onChange={(evento) => setDesde(evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="perfil-hasta">Hasta</label>
          <input
            id="perfil-hasta"
            type="date"
            value={hasta}
            onChange={(evento) => setHasta(evento.target.value)}
          />
          <span className="campo-ayuda">Obligatorio: no existe perfil de vigencia indefinida.</span>
        </div>

        <button
          type="button"
          className="primario"
          disabled={tipoPersonaId === '' || desde === '' || hasta === '' || asignar.isPending}
          onClick={() =>
            asignar.mutate(
              {
                tipoPersonaId,
                fechaHoraInicio: new Date(`${desde}T00:00:00Z`).toISOString(),
                fechaHoraFin: new Date(`${hasta}T00:00:00Z`).toISOString(),
              },
              { onSuccess: () => setTipoPersonaId('') },
            )
          }
        >
          {asignar.isPending ? 'Asignando…' : 'Asignar perfil'}
        </button>
      </div>
    </section>
  )
}
