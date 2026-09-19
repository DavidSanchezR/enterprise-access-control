import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import type { Usuario } from './api'
import { useAlcanceUsuario, useReemplazarAlcance } from './hooks'

/**
 * Reemplazo del alcance de compañías administrables por un usuario (RF-005, RF-050).
 *
 * La operación del contrato es un reemplazo completo (`PUT`), no un alta/baja incremental: la
 * pantalla lo refleja editando la lista entera y avisando de que lo que se guarde sustituye al
 * alcance actual.
 */
export function AlcanceCompaniasDialogo({
  usuario,
  alCerrar,
}: {
  usuario: Usuario
  alCerrar: () => void
}): ReactElement {
  const consulta = useAlcanceUsuario(usuario.id)
  const reemplazar = useReemplazarAlcance()

  /**
   * `null` significa "el usuario todavía no ha escrito nada": el cuadro muestra entonces el alcance
   * que devuelve el servidor. Se deriva en el renderizado en lugar de copiarlo a estado desde un
   * efecto, que provocaría un render en cascada y podría pisar lo que ya estuviera escrito.
   */
  const [edicion, setEdicion] = useState<string | null>(null)
  const texto = edicion ?? consulta.data?.join('\n') ?? ''

  const error = reemplazar.error instanceof ApiError ? reemplazar.error : undefined
  const errorCarga = consulta.error instanceof ApiError ? consulta.error : undefined

  const ids = texto
    .split(/[\s,;]+/)
    .map((valor) => valor.trim())
    .filter((valor) => valor.length > 0)

  return (
    <Dialogo titulo={`Alcance de ${usuario.correo}`} alCerrar={alCerrar}>
      {errorCarga && (
        <p className="aviso error" role="alert">
          {errorCarga.message}
        </p>
      )}

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      <div className="campo">
        <label htmlFor="alcance-companias">Compañías administrables</label>
        <textarea
          id="alcance-companias"
          rows={6}
          value={texto}
          onChange={(evento) => setEdicion(evento.target.value)}
          aria-describedby="alcance-ayuda"
          disabled={consulta.isPending}
        />
        <span className="campo-ayuda" id="alcance-ayuda">
          Un identificador por línea. Lo que guarde reemplaza por completo el alcance actual
          {ids.length === 0 ? '; dejarlo vacío retira todas las compañías.' : '.'}
        </span>
      </div>

      <p aria-live="polite" className="alcance-resumen">
        {consulta.isPending
          ? 'Cargando alcance…'
          : `${ids.length} compañía${ids.length === 1 ? '' : 's'} en el alcance.`}
      </p>

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cancelar
        </button>
        <button
          type="button"
          className="primario"
          disabled={reemplazar.isPending || consulta.isPending}
          onClick={() =>
            reemplazar.mutate({ id: usuario.id, companiaIds: ids }, { onSuccess: alCerrar })
          }
        >
          {reemplazar.isPending ? 'Guardando…' : 'Reemplazar alcance'}
        </button>
      </div>
    </Dialogo>
  )
}
