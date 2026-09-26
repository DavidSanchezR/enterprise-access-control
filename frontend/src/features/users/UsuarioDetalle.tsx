import { useState, type ReactElement } from 'react'
import { Dialogo } from '../../components/Dialogo'
import { aIsoUtc, aValorLocal, formatearFechaHora } from '../../lib/fechas'
import { useCompanias } from '../companies/hooks'
import { etiquetaRol } from './CamposAsignacion'
import { useFinalizarRol, useRenovarRol, useRolesUsuario } from './hooks'
import { describirError } from './mensajesRol'
import type { AsignacionRol, Usuario } from './api'

const TABS = ['Resumen', 'Asignaciones', 'Histórico'] as const
type Tab = (typeof TABS)[number]

/**
 * Detalle de usuario con sus asignaciones (UX-18, UX-20; ux-ui.md §35 y §16).
 *
 * En la pestaña de asignaciones se distinguen **dos dimensiones que no se fusionan** (§16): que la
 * asignación exista como registro, y que esté vigente hoy según sus fechas. Una asignación puede
 * figurar registrada y no estar vigente; presentarlas como un solo estado ocultaría justamente el
 * caso que se necesita ver.
 */
export function UsuarioDetalle({
  usuario,
  alCerrar,
}: {
  usuario: Usuario
  alCerrar: () => void
}): ReactElement {
  const [tab, setTab] = useState<Tab>('Resumen')
  const [aFinalizar, setAFinalizar] = useState<AsignacionRol | null>(null)
  const [aRenovar, setARenovar] = useState<AsignacionRol | null>(null)

  const roles = useRolesUsuario(usuario.id)
  const finalizar = useFinalizarRol()
  const renovar = useRenovarRol()

  // Las asignaciones solo traen `companiaId`. El nombre sale de la misma consulta que mantiene
  // `UsuariosPage` (mismo filtro, misma caché), sin llamadas adicionales. Nunca se muestra el UUID
  // (RF-013): una compañía inactiva o fuera del alcance actual queda como "no disponible" (RF-077).
  const companias = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

  const nombreCompania = (id: string): ReactElement | string =>
    companias.data?.items.find((c) => c.id === id)?.nombre ?? (
      <span className="sin-resolver">Compañía no disponible</span>
    )

  const error =
    describirError(roles.error) ??
    describirError(finalizar.error) ??
    describirError(renovar.error)

  return (
    <Dialogo titulo={usuario.correo} alCerrar={alCerrar}>
      <div className="tabs" role="tablist" aria-label="Secciones del usuario">
        {TABS.map((nombre) => (
          <button
            key={nombre}
            type="button"
            role="tab"
            id={`tab-${nombre}`}
            aria-selected={tab === nombre}
            aria-controls={`panel-${nombre}`}
            className={tab === nombre ? 'tab actual' : 'tab'}
            onClick={() => setTab(nombre)}
          >
            {nombre}
          </button>
        ))}
      </div>

      {error && (
        <p className="aviso error" role="alert">
          {error.mensaje}
        </p>
      )}

      {tab === 'Resumen' && (
        <dl className="detalle-resumen" id="panel-Resumen" role="tabpanel" aria-labelledby="tab-Resumen">
          <dt>Correo</dt>
          <dd>{usuario.correo}</dd>

          <dt>Estado</dt>
          <dd>
            <span className={`etiqueta ${usuario.estado}`}>{usuario.estado}</span>
          </dd>

          <dt>Cambio de contraseña</dt>
          <dd>{usuario.requiereCambioPassword ? 'Pendiente' : 'Al día'}</dd>
        </dl>
      )}

      {tab === 'Asignaciones' && (
        <div id="panel-Asignaciones" role="tabpanel" aria-labelledby="tab-Asignaciones">
          {roles.isPending && <p>Cargando asignaciones…</p>}

          {!roles.isPending && roles.data?.length === 0 && (
            <p>Este usuario no tiene ninguna asignación registrada.</p>
          )}

          {roles.data && roles.data.length > 0 && (
            <table>
              <caption className="sr-only">
                Asignaciones de rol del usuario, vigentes e históricas
              </caption>
              <thead>
                <tr>
                  <th scope="col">Rol</th>
                  <th scope="col">Compañía</th>
                  <th scope="col">Inicio</th>
                  <th scope="col">Fin</th>
                  <th scope="col">Vigente hoy</th>
                  <th scope="col">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {roles.data.map((asignacion) => (
                  <tr key={asignacion.id}>
                    <td>{etiquetaRol(asignacion.rol)}</td>
                    {/* El alcance global no tiene compañía: se dice, no se deja en blanco. */}
                    <td>
                      {asignacion.companiaId
                        ? nombreCompania(asignacion.companiaId)
                        : 'Todas (alcance global)'}
                    </td>
                    <td>{formatearFechaHora(asignacion.fechaHoraInicio)}</td>
                    <td>{formatearFechaHora(asignacion.fechaHoraFin)}</td>
                    <td>
                      <span className={asignacion.vigente ? 'etiqueta ACTIVO' : 'etiqueta INACTIVO'}>
                        {asignacion.vigente ? 'Sí' : 'No'}
                      </span>
                    </td>
                    <td>
                      {/* Ambas acciones exigen vigencia: una asignación expirada no se finaliza ni
                          se renueva — requiere una asignación nueva (RF-075). */}
                      {asignacion.vigente && (
                        <>
                          <button type="button" onClick={() => setARenovar(asignacion)}>
                            Renovar
                          </button>
                          <button type="button" onClick={() => setAFinalizar(asignacion)}>
                            Finalizar
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

        </div>
      )}

      {tab === 'Histórico' && (
        <div id="panel-Histórico" role="tabpanel" aria-labelledby="tab-Histórico">
          <p>
            La trazabilidad de altas, finalizaciones y renovaciones se deriva de las fechas de cada
            asignación, que se conservan íntegras: ninguna se elimina al finalizarla.
          </p>

          {roles.data && (
            <ul className="historico-lista">
              {[...roles.data]
                .sort((a, b) => b.fechaHoraInicio.localeCompare(a.fechaHoraInicio))
                .map((asignacion) => (
                  <li key={asignacion.id}>
                    {formatearFechaHora(asignacion.fechaHoraInicio)} — {etiquetaRol(asignacion.rol)}
                    {asignacion.companiaId ? (
                      <> · {nombreCompania(asignacion.companiaId)}</>
                    ) : (
                      ' · alcance global'
                    )}
                    {` · hasta ${formatearFechaHora(asignacion.fechaHoraFin)}`}
                  </li>
                ))}
            </ul>
          )}
        </div>
      )}

      {aFinalizar && (
        <ConfirmarFinalizacion
          asignacion={aFinalizar}
          enCurso={finalizar.isPending}
          alConfirmar={() =>
            finalizar.mutate(
              { id: usuario.id, asignacionId: aFinalizar.id },
              { onSuccess: () => setAFinalizar(null) },
            )
          }
          alCancelar={() => setAFinalizar(null)}
        />
      )}

      {aRenovar && (
        <ConfirmarRenovacion
          asignacion={aRenovar}
          enCurso={renovar.isPending}
          alConfirmar={(fechaHoraFin) =>
            renovar.mutate(
              { id: usuario.id, asignacionId: aRenovar.id, fechaHoraFin },
              { onSuccess: () => setARenovar(null) },
            )
          }
          alCancelar={() => setARenovar(null)}
        />
      )}

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cerrar
        </button>
      </div>
    </Dialogo>
  )
}

/** Finalizar es una acción crítica: exige confirmación explícita (ux-ui.md §31.13). */
function ConfirmarFinalizacion({
  asignacion,
  enCurso,
  alConfirmar,
  alCancelar,
}: {
  asignacion: AsignacionRol
  enCurso: boolean
  alConfirmar: () => void
  alCancelar: () => void
}): ReactElement {
  return (
    <div className="confirmacion" role="alertdialog" aria-label="Confirmar finalización">
      <p>
        Se cerrará la vigencia de <strong>{etiquetaRol(asignacion.rol)}</strong>
        {asignacion.companiaId ? ' en esa compañía' : ' con alcance global'} en este momento. El
        registro se conserva en el histórico.
      </p>

      <div className="dialogo-acciones">
        <button type="button" onClick={alCancelar}>
          Cancelar
        </button>
        <button type="button" className="peligro" disabled={enCurso} onClick={alConfirmar}>
          {enCurso ? 'Finalizando…' : 'Finalizar asignación'}
        </button>
      </div>
    </div>
  )
}

/**
 * Renovar extiende la vigencia sin crear una asignación nueva (UX-20, RF-075).
 *
 * Exige confirmación igual que finalizar, y valida en el cliente lo mismo que el servidor rechazará
 * con `409 RENOVACION_NO_POSTERIOR`: la nueva fecha debe ser estrictamente posterior a la vigente. La
 * validación del cliente es ayuda de UX, no la frontera — el backend decide (Principio I).
 */
function ConfirmarRenovacion({
  asignacion,
  enCurso,
  alConfirmar,
  alCancelar,
}: {
  asignacion: AsignacionRol
  enCurso: boolean
  alConfirmar: (fechaHoraFin: string) => void
  alCancelar: () => void
}): ReactElement {
  const [valor, setValor] = useState(aValorLocal(asignacion.fechaHoraFin))

  const vigenteLocal = aValorLocal(asignacion.fechaHoraFin)
  const noPosterior = valor !== '' && valor <= vigenteLocal

  return (
    <div className="confirmacion" role="alertdialog" aria-label="Confirmar renovación">
      <p>
        Se extenderá la vigencia de <strong>{etiquetaRol(asignacion.rol)}</strong>
        {asignacion.companiaId ? ' en esa compañía' : ' con alcance global'}. No cambia el rol ni la
        compañía, y no se crea una asignación nueva.
      </p>

      <div className="campo">
        <label htmlFor="renovar-fin">Nuevo fin de vigencia</label>
        <input
          id="renovar-fin"
          type="datetime-local"
          value={valor}
          aria-invalid={noPosterior ? 'true' : undefined}
          aria-describedby={noPosterior ? 'renovar-fin-error' : undefined}
          onChange={(evento) => setValor(evento.target.value)}
        />
        <p className="campo-ayuda">
          Vigente hasta {formatearFechaHora(asignacion.fechaHoraFin)}.
        </p>
        {noPosterior && (
          <span className="error-campo" id="renovar-fin-error" role="alert">
            La nueva fecha de fin debe ser posterior a la vigente.
          </span>
        )}
      </div>

      <div className="dialogo-acciones">
        <button type="button" onClick={alCancelar}>
          Cancelar
        </button>
        <button
          type="button"
          className="primario"
          disabled={enCurso || valor === '' || noPosterior}
          onClick={() => alConfirmar(aIsoUtc(valor))}
        >
          {enCurso ? 'Renovando…' : 'Renovar asignación'}
        </button>
      </div>
    </div>
  )
}
