import { useState, type ReactElement } from 'react'
import { ApiError } from '../../lib/apiClient'
import { useCompanias } from '../companies/hooks'
import { useMaestro } from '../masters/hooks'
import type { ContextoOperativo } from '../people/history/api'
import { useContextos } from '../people/history/hooks'
import { ETIQUETA_ESTADO_CREDENCIAL, estaVigente, type AsignacionCredencial } from './api'
import {
  useAsignarCredencial,
  useCredenciales,
  useDevolverCredencial,
  useEliminarCredencial,
} from './hooks'
import './credentials.css'

const MENSAJE_POR_CODIGO: Record<string, string> = {
  SIN_CONTEXTO_OPERATIVO_VIGENTE:
    'La persona no tiene un contexto operativo vigente con esa compañía principal.',
  SOLAPAMIENTO_VIGENCIA:
    'La persona ya tiene una credencial asignada para esa compañía principal en ese periodo. Devuélvala o désela de baja antes de asignar otra.',
  FUERA_DE_CONTENCION_TEMPORAL:
    'La vigencia de la credencial excede la pertenencia de la persona a su compañía.',
  VALOR_MAESTRO_INACTIVO: 'El tipo de credencial no existe o está inactivo.',
  COMPANIA_DEBE_SER_PRINCIPAL: 'Las credenciales solo se emiten para compañías principales.',
  CREDENCIAL_NO_ASIGNADA:
    'La credencial ya no está asignada: no puede devolverse ni darse de baja.',
}

function mensaje(error: ApiError): string {
  return (error.codigo && MENSAJE_POR_CODIGO[error.codigo]) || error.message
}

function formatearFecha(iso: string): string {
  return new Date(iso).toLocaleDateString()
}

/** Contexto activo y dentro de su ventana hoy: el mismo criterio con que el servidor lo exige. */
function contextoVigente(contexto: ContextoOperativo): boolean {
  const ahora = Date.now()

  return (
    contexto.estado === 'ACTIVO' &&
    new Date(contexto.fechaHoraInicio).getTime() <= ahora &&
    ahora < new Date(contexto.fechaHoraFin).getTime()
  )
}

/**
 * Credenciales de una persona agrupadas por Compañía Principal (Historia 9; RF-018, RF-056 a RF-058).
 *
 * Cada Principal tiene su propio histórico: una persona puede portar credenciales vigentes de varias
 * a la vez (CS-016, CS-017), y la de una no sirve para otra (CS-024). Por eso la pantalla nunca
 * muestra una "credencial de la persona" global.
 *
 * Solo una credencial ASIGNADO admite acciones; los demás estados son terminales. Dar de baja no
 * borra: el registro sigue en el histórico con estado "Dada de baja".
 */
export function CredencialesPersona({ personaId }: { personaId: string }): ReactElement {
  const credenciales = useCredenciales(personaId)
  const contextos = useContextos(personaId)
  const tipos = useMaestro('tipos-credencial')
  const principales = useCompanias({ tipoCompania: 'PRINCIPAL_MANDANTE', tamañoPagina: 200 })

  const asignar = useAsignarCredencial(personaId)
  const devolver = useDevolverCredencial(personaId)
  const eliminar = useEliminarCredencial(personaId)

  const [principalId, setPrincipalId] = useState('')
  const [tipoId, setTipoId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [confirmandoBaja, setConfirmandoBaja] = useState<string | null>(null)

  const error =
    (asignar.error instanceof ApiError && asignar.error) ||
    (devolver.error instanceof ApiError && devolver.error) ||
    (eliminar.error instanceof ApiError && eliminar.error) ||
    (credenciales.error instanceof ApiError && credenciales.error) ||
    undefined

  const nombrePrincipal = (id: string): string =>
    principales.data?.items.find((c) => c.id === id)?.nombre ?? id

  const nombreTipo = (id: string): string => tipos.data?.find((t) => t.id === id)?.nombre ?? id

  // RF-056: solo se ofrecen las Principales con contexto operativo vigente, que es lo que el
  // servidor exige para emitir la credencial.
  const principalesConContexto = [
    ...new Set((contextos.data ?? []).filter(contextoVigente).map((c) => c.companiaPrincipalId)),
  ]

  // Agrupadas por Principal conservando el orden del servidor (inicio más reciente primero).
  const grupos = new Map<string, AsignacionCredencial[]>()
  for (const credencial of credenciales.data ?? []) {
    grupos.set(credencial.companiaPrincipalId, [
      ...(grupos.get(credencial.companiaPrincipalId) ?? []),
      credencial,
    ])
  }

  const tiposActivos = (tipos.data ?? []).filter((t) => t.estado === 'ACTIVO')
  const listo = principalId !== '' && tipoId !== '' && desde !== '' && hasta !== ''

  return (
    <div className="credenciales">
      <h2 className="historial-seccion">Credenciales</h2>
      <p className="historial-ayuda">
        Cada compañía principal emite su propia credencial. El tipo indica el diseño de la
        credencial, no una tecnología de identificación.
      </p>

      {error && (
        <p className="aviso error" role="alert">
          {mensaje(error)}
        </p>
      )}

      {credenciales.isPending && <p>Cargando credenciales…</p>}

      {!credenciales.isPending && grupos.size === 0 && (
        <p className="historial-vacio">Esta persona no tiene credenciales registradas.</p>
      )}

      {[...grupos.entries()].map(([principal, delGrupo]) => (
        <section
          key={principal}
          className="credenciales-grupo"
          aria-labelledby={`credenciales-${principal}`}
        >
          <h3 id={`credenciales-${principal}`}>{nombrePrincipal(principal)}</h3>

          <ol className="linea-tiempo">
            {delGrupo.map((credencial) => {
              const vigente = estaVigente(credencial)

              return (
                <li key={credencial.id} className="tarjeta linea-tiempo-item">
                  <div className="linea-tiempo-cabecera">
                    <strong>{nombreTipo(credencial.tipoCredencialId)}</strong>

                    <span className={`etiqueta credencial-${credencial.estado}`}>
                      {ETIQUETA_ESTADO_CREDENCIAL[credencial.estado]}
                    </span>

                    <span className={`etiqueta ${vigente ? 'ACTIVO' : 'INACTIVO'}`}>
                      {vigente ? 'Vigente hoy' : 'No vigente'}
                    </span>
                  </div>

                  <p className="linea-tiempo-fechas">
                    {formatearFecha(credencial.fechaHoraInicio)} –{' '}
                    {formatearFecha(credencial.fechaHoraFin)}
                  </p>

                  {credencial.estado === 'REVOCADA' && (
                    <p className="linea-tiempo-motivo">
                      Revocada automáticamente al cesar la pertenencia
                      {credencial.revocadoPorPertenenciaId && (
                        <span className="credencial-referencia">
                          {' '}
                          (pertenencia <code>{credencial.revocadoPorPertenenciaId}</code>)
                        </span>
                      )}
                    </p>
                  )}

                  {credencial.estado === 'ASIGNADO' && (
                    <div className="linea-tiempo-acciones">
                      <button
                        type="button"
                        disabled={devolver.isPending}
                        onClick={() => devolver.mutate(credencial.id)}
                      >
                        Devolver
                      </button>

                      {confirmandoBaja === credencial.id ? (
                        <>
                          <button
                            type="button"
                            className="peligro"
                            disabled={eliminar.isPending}
                            onClick={() =>
                              eliminar.mutate(credencial.id, {
                                onSettled: () => setConfirmandoBaja(null),
                              })
                            }
                          >
                            Confirmar baja
                          </button>
                          <button type="button" onClick={() => setConfirmandoBaja(null)}>
                            Cancelar
                          </button>
                        </>
                      ) : (
                        <button
                          type="button"
                          className="peligro"
                          onClick={() => setConfirmandoBaja(credencial.id)}
                        >
                          Dar de baja
                        </button>
                      )}
                    </div>
                  )}
                </li>
              )
            })}
          </ol>
        </section>
      ))}

      <form
        className="tarjeta credenciales-alta"
        onSubmit={(evento) => {
          evento.preventDefault()
          if (!listo) {
            return
          }

          asignar.mutate(
            {
              companiaPrincipalId: principalId,
              tipoCredencialId: tipoId,
              // El servidor normaliza a días completos; se envía la fecha tal cual se eligió.
              fechaHoraInicio: new Date(`${desde}T00:00:00Z`).toISOString(),
              fechaHoraFin: new Date(`${hasta}T00:00:00Z`).toISOString(),
            },
            {
              onSuccess: () => {
                setTipoId('')
                setDesde('')
                setHasta('')
              },
            },
          )
        }}
      >
        <h3>Asignar credencial</h3>

        {principalesConContexto.length === 0 && !contextos.isPending && (
          <p className="historial-vacio">
            Para asignar una credencial la persona necesita un contexto operativo vigente con la
            compañía principal.
          </p>
        )}

        <div className="credenciales-campos">
          <div className="campo">
            <label htmlFor="credencial-principal">Compañía principal</label>
            <select
              id="credencial-principal"
              value={principalId}
              onChange={(evento) => setPrincipalId(evento.target.value)}
            >
              <option value="">Seleccione…</option>
              {principalesConContexto.map((id) => (
                <option key={id} value={id}>
                  {nombrePrincipal(id)}
                </option>
              ))}
            </select>
          </div>

          <div className="campo">
            <label htmlFor="credencial-tipo">Tipo de credencial</label>
            <select
              id="credencial-tipo"
              value={tipoId}
              onChange={(evento) => setTipoId(evento.target.value)}
            >
              <option value="">Seleccione…</option>
              {tiposActivos.map((tipo) => (
                <option key={tipo.id} value={tipo.id}>
                  {tipo.nombre}
                </option>
              ))}
            </select>
          </div>

          <div className="campo">
            <label htmlFor="credencial-desde">Desde</label>
            <input
              id="credencial-desde"
              type="date"
              required
              value={desde}
              onChange={(evento) => setDesde(evento.target.value)}
            />
          </div>

          <div className="campo">
            <label htmlFor="credencial-hasta">Hasta</label>
            <input
              id="credencial-hasta"
              type="date"
              required
              min={desde || undefined}
              value={hasta}
              aria-describedby="credencial-hasta-ayuda"
              onChange={(evento) => setHasta(evento.target.value)}
            />
            <span className="campo-ayuda" id="credencial-hasta-ayuda">
              Obligatorio: toda credencial tiene fecha de término.
            </span>
          </div>
        </div>

        <button type="submit" className="primario" disabled={!listo || asignar.isPending}>
          {asignar.isPending ? 'Asignando…' : 'Asignar credencial'}
        </button>
      </form>
    </div>
  )
}
