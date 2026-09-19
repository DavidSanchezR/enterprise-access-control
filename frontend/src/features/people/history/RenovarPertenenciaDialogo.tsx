import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../../components/Dialogo'
import { ApiError } from '../../../lib/apiClient'
import type { AsignacionCompania } from './api'
import { useRenovarPertenencia } from './hooks'

/**
 * Renovación de una pertenencia vigente (RF-073).
 *
 * Extiende la fecha de fin sin cerrar nada: es la contraparte inversa de finalizar. La pantalla lo
 * dice explícitamente porque las dos acciones viven juntas y confundirlas tendría consecuencias
 * opuestas —una amplía el acceso, la otra lo revoca en cascada—.
 */
export function RenovarPertenenciaDialogo({
  personaId,
  pertenencia,
  alCerrar,
}: {
  personaId: string
  pertenencia: AsignacionCompania
  alCerrar: () => void
}): ReactElement {
  const renovar = useRenovarPertenencia(personaId)

  const finActual = pertenencia.fechaHoraFin.slice(0, 10)
  const [nuevaFecha, setNuevaFecha] = useState('')

  const error = renovar.error instanceof ApiError ? renovar.error : undefined

  const mensajeError =
    error?.codigo === 'RENOVACION_NO_POSTERIOR'
      ? 'La nueva fecha debe ser posterior a la vigencia actual.'
      : error?.codigo === 'PERTENENCIA_NO_RENOVABLE'
        ? 'Esta pertenencia ya no es renovable: fue finalizada o su vigencia expiró. Registre una nueva pertenencia.'
        : error?.message

  return (
    <Dialogo titulo="Renovar pertenencia" alCerrar={alCerrar}>
      {error && (
        <p className="aviso error" role="alert">
          {mensajeError}
        </p>
      )}

      <p className="campo-ayuda">
        Vigencia actual hasta el{' '}
        <strong>{new Date(pertenencia.fechaHoraFin).toLocaleDateString()}</strong>. Renovar solo
        amplía esa fecha: no cierra la pertenencia ni afecta a los contextos, unidades o
        credenciales ya existentes.
      </p>

      <div className="campo">
        <label htmlFor="nueva-fecha-fin">Nueva fecha de fin</label>
        <input
          id="nueva-fecha-fin"
          type="date"
          min={finActual}
          value={nuevaFecha}
          onChange={(evento) => setNuevaFecha(evento.target.value)}
          aria-describedby="nueva-fecha-ayuda"
        />
        <span className="campo-ayuda" id="nueva-fecha-ayuda">
          Debe ser posterior a la fecha de fin vigente.
        </span>
      </div>

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cancelar
        </button>
        <button
          type="button"
          className="primario"
          disabled={nuevaFecha === '' || renovar.isPending}
          onClick={() =>
            renovar.mutate(
              {
                asignacionId: pertenencia.id,
                // El servidor normaliza al fin del día; se envía la fecha tal cual se eligió.
                fechaHoraFin: new Date(`${nuevaFecha}T00:00:00Z`).toISOString(),
              },
              { onSuccess: alCerrar },
            )
          }
        >
          {renovar.isPending ? 'Renovando…' : 'Renovar'}
        </button>
      </div>
    </Dialogo>
  )
}
