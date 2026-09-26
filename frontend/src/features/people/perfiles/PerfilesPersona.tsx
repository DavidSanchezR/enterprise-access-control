import { useState, type ReactElement } from 'react'
import { ApiError } from '../../../lib/apiClient'
import { formatearFechaHora } from '../../../lib/fechas'
import { CodigosError } from '../../../lib/problemDetails'
import { useMaestro } from '../../masters/hooks'
import { useHistorialCompanias } from '../history/hooks'
import { pertenenciaVigente } from '../history/pertenenciaVigente'
import { useAsignarPerfil, usePerfiles } from './hooks'

function mensajeDeError(error: ApiError): string {
  switch (error.codigo) {
    case CodigosError.SOLAPAMIENTO_VIGENCIA:
      return 'Ya existe un perfil vigente para ese intervalo; ciérrelo antes o ajuste las fechas.'
    case CodigosError.FUERA_DE_CONTENCION_TEMPORAL:
      return 'La vigencia del perfil debe quedar dentro de la pertenencia vigente de la persona.'
    case CodigosError.SIN_PERTENENCIA_VIGENTE:
      return 'La persona no tiene una pertenencia vigente: registre o renueve su pertenencia antes de asignarle un perfil.'
    default:
      return error.message
  }
}

/**
 * Perfiles vigentes e históricos de una persona (RF-011, `AsignaciónTipoPersona`).
 *
 * Un perfil no está ligado a ninguna compañía, así que su vigencia no es resoluble a una única
 * Compañía Principal: las fechas se presentan en la zona horaria global de respaldo (RF-080).
 *
 * Desde el cambio post-Baseline VF-007 (RF-082), su vigencia debe quedar dentro de la pertenencia
 * vigente. Los límites de fecha y el aviso sin pertenencia solo orientan: la regla la aplica el
 * servidor, que responde con un código específico si no se cumple.
 */
export function PerfilesPersona({ personaId }: { personaId: string }): ReactElement {
  const perfiles = usePerfiles(personaId)
  const tipos = useMaestro('tipos-persona', 'ACTIVO')
  const asignar = useAsignarPerfil(personaId)
  const historial = useHistorialCompanias(personaId)

  const [tipoPersonaId, setTipoPersonaId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')

  const error = asignar.error instanceof ApiError ? asignar.error : undefined

  // Solo se afirma que no hay pertenencia cuando el historial se leyó bien. Si la consulta falla, la
  // interfaz no sabe nada y deja decidir al servidor.
  const vigente = pertenenciaVigente(historial.data)
  const sinPertenencia = historial.isSuccess && vigente === undefined

  // Las pertenencias se normalizan a días completos en UTC, igual que los perfiles: la fecha UTC de
  // cada borde es el límite exacto del control de tipo fecha.
  const minimo = vigente?.fechaHoraInicio.slice(0, 10)
  const maximo = vigente?.fechaHoraFin.slice(0, 10)

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
          {mensajeDeError(error)}
        </p>
      )}

      {sinPertenencia && (
        <p className="aviso" role="status">
          La persona no tiene una pertenencia vigente. Un perfil solo puede asignarse dentro de la
          vigencia de su pertenencia a una compañía.
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
            min={minimo}
            max={maximo}
            value={desde}
            onChange={(evento) => setDesde(evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="perfil-hasta">Hasta</label>
          <input
            id="perfil-hasta"
            type="date"
            min={minimo}
            max={maximo}
            value={hasta}
            onChange={(evento) => setHasta(evento.target.value)}
          />
          <span className="campo-ayuda">Obligatorio: no existe perfil de vigencia indefinida.</span>
          {vigente && (
            <span className="campo-ayuda">
              Debe quedar dentro de la pertenencia vigente: del {minimo} al {maximo}.
            </span>
          )}
        </div>

        <button
          type="button"
          className="primario"
          disabled={
            tipoPersonaId === '' ||
            desde === '' ||
            hasta === '' ||
            sinPertenencia ||
            asignar.isPending
          }
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
