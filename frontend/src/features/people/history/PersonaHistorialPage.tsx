import { useState, type ReactElement } from 'react'
import { useParams } from 'react-router-dom'
import { ApiError } from '../../../lib/apiClient'
import { useCompanias } from '../../companies/hooks'
import { CredencialesPersona } from '../../credentials/CredencialesPersona'
import { AsignacionUnidadOrganizativa } from '../AsignacionUnidadOrganizativa/AsignacionUnidadOrganizativa'
import {
  ETIQUETA_MOTIVO_PERTENENCIA,
  ETIQUETA_MOTIVO_REVOCACION,
  type AsignacionCompania,
  type ContextoOperativo,
} from './api'
import { PerfilesPersona } from '../perfiles/PerfilesPersona'
import { PertenenciaContextoWizard } from './PertenenciaContextoWizard'
import { RenovarPertenenciaDialogo } from './RenovarPertenenciaDialogo'
import { useContextos, useFinalizarPertenencia, useHistorialCompanias } from './hooks'
import './history.css'

function formatearFecha(iso: string): string {
  return new Date(iso).toLocaleDateString()
}

/** Vigencia efectiva por fechas, que es como la determina el servidor (Principio IV). */
function estaVigente(desde: string, hasta: string): boolean {
  const ahora = Date.now()
  return new Date(desde).getTime() <= ahora && ahora < new Date(hasta).getTime()
}

/**
 * Histórico de una persona: línea de tiempo de pertenencias y de contextos operativos
 * (Historia 5, RF-014, RF-037, RF-061, RF-073).
 *
 * Distingue dos cosas que es fácil confundir: el `estado` es administrativo —explica *por qué*
 * terminó algo— mientras que la vigencia se deriva de las fechas. Un registro puede estar marcado
 * como revocado y seguir vigente hasta una fecha futura (RF-064), y la pantalla lo muestra así en
 * lugar de darlo por terminado antes de tiempo.
 */
export function PersonaHistorialPage(): ReactElement {
  const { personaId = '' } = useParams<{ personaId: string }>()

  const [renovando, setRenovando] = useState<AsignacionCompania | null>(null)
  const [contextoSeleccionado, setContextoSeleccionado] = useState<ContextoOperativo | null>(null)
  const [registrando, setRegistrando] = useState(false)

  const historial = useHistorialCompanias(personaId)
  const contextos = useContextos(personaId)
  const finalizar = useFinalizarPertenencia(personaId)

  const principales = useCompanias({ tipoCompania: 'PRINCIPAL_MANDANTE', tamañoPagina: 200 })
  const todas = useCompanias({ tamañoPagina: 200 })

  const error =
    (historial.error instanceof ApiError && historial.error) ||
    (finalizar.error instanceof ApiError && finalizar.error) ||
    undefined

  const nombreCompania = (id: string): string =>
    todas.data?.items.find((c) => c.id === id)?.nombre ?? id

  const nombrePrincipal = (id: string): string =>
    principales.data?.items.find((c) => c.id === id)?.nombre ?? nombreCompania(id)

  return (
    <section className="historial">
      <header>
        <h1>Histórico de la persona</h1>
        <p className="historial-subtitulo">
          Pertenencia a compañía, contextos operativos y credenciales. Cerrar una pertenencia revoca
          en cascada todo lo que dependía de ella.
        </p>
      </header>

      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      {/* Punto de entrada que faltaba: los hooks de alta de pertenencia y apertura de contexto ya
          existían, pero ninguna pantalla los invocaba y Historia 5 solo era operable por API (D7). */}
      <div className="historial-acciones">
        <button type="button" className="primario" onClick={() => setRegistrando(true)}>
          Registrar pertenencia y contexto
        </button>
      </div>

      <h2>Pertenencia a compañía</h2>

      {historial.isPending && <p>Cargando histórico…</p>}

      {!historial.isPending && historial.data?.length === 0 && (
        <p className="historial-vacio">
          Esta persona todavía no tiene ninguna pertenencia registrada.
        </p>
      )}

      <ol className="linea-tiempo">
        {historial.data?.map((pertenencia) => {
          const vigente = estaVigente(pertenencia.fechaHoraInicio, pertenencia.fechaHoraFin)

          return (
            <li key={pertenencia.id} className="tarjeta linea-tiempo-item">
              <div className="linea-tiempo-cabecera">
                <strong>{nombreCompania(pertenencia.companiaId)}</strong>

                {/* Estado y vigencia son cosas distintas: ambas se muestran como texto. */}
                <span
                  className={`etiqueta ${pertenencia.estado === 'ACTIVA' ? 'ACTIVO' : 'INACTIVO'}`}
                >
                  {pertenencia.estado}
                </span>

                <span className={`etiqueta ${vigente ? 'ACTIVO' : 'INACTIVO'}`}>
                  {vigente ? 'Vigente hoy' : 'Fuera de vigencia'}
                </span>
              </div>

              <p className="linea-tiempo-fechas">
                {formatearFecha(pertenencia.fechaHoraInicio)} –{' '}
                {formatearFecha(pertenencia.fechaHoraFin)}
              </p>

              {pertenencia.motivoFin && (
                <p className="linea-tiempo-motivo">
                  {ETIQUETA_MOTIVO_PERTENENCIA[pertenencia.motivoFin]}
                </p>
              )}

              {pertenencia.estado === 'ACTIVA' && vigente && (
                <div className="linea-tiempo-acciones">
                  <button type="button" onClick={() => setRenovando(pertenencia)}>
                    Renovar
                  </button>

                  <button
                    type="button"
                    className="peligro"
                    disabled={finalizar.isPending}
                    onClick={() =>
                      finalizar.mutate({
                        asignacionId: pertenencia.id,
                        fechaHoraFin: new Date().toISOString(),
                      })
                    }
                  >
                    Finalizar
                  </button>
                </div>
              )}
            </li>
          )
        })}
      </ol>

      <h2 className="historial-seccion">Contextos operativos</h2>

      <p className="historial-ayuda">
        Una persona puede operar con varias compañías principales a la vez, cada una con su propia
        unidad organizativa.
      </p>

      {!contextos.isPending && contextos.data?.length === 0 && (
        <p className="historial-vacio">Todavía no hay contextos operativos abiertos.</p>
      )}

      <ol className="linea-tiempo">
        {contextos.data?.map((contexto) => {
          const vigente = estaVigente(contexto.fechaHoraInicio, contexto.fechaHoraFin)

          return (
            <li key={contexto.id} className="tarjeta linea-tiempo-item">
              <div className="linea-tiempo-cabecera">
                <strong>{nombrePrincipal(contexto.companiaPrincipalId)}</strong>

                <span className={`etiqueta ${contexto.estado}`}>{contexto.estado}</span>

                <span className={`etiqueta ${vigente ? 'ACTIVO' : 'INACTIVO'}`}>
                  {vigente ? 'Vigente hoy' : 'Fuera de vigencia'}
                </span>
              </div>

              <p className="linea-tiempo-fechas">
                {formatearFecha(contexto.fechaHoraInicio)} – {formatearFecha(contexto.fechaHoraFin)}
              </p>

              {contexto.motivoFin && (
                <p className="linea-tiempo-motivo">
                  {ETIQUETA_MOTIVO_REVOCACION[contexto.motivoFin]}
                  {contexto.revocadoPorPertenenciaId && ' (revocación automática en cascada)'}
                </p>
              )}

              <div className="linea-tiempo-acciones">
                <button
                  type="button"
                  onClick={() =>
                    setContextoSeleccionado(
                      contextoSeleccionado?.id === contexto.id ? null : contexto,
                    )
                  }
                  aria-expanded={contextoSeleccionado?.id === contexto.id}
                >
                  Unidad organizativa
                </button>
              </div>

              {contextoSeleccionado?.id === contexto.id && (
                <AsignacionUnidadOrganizativa personaId={personaId} contexto={contexto} />
              )}
            </li>
          )
        })}
      </ol>

      <PerfilesPersona personaId={personaId} />

      <CredencialesPersona personaId={personaId} />

      {registrando && (
        <PertenenciaContextoWizard
          personaId={personaId}
          alCerrar={() => setRegistrando(false)}
        />
      )}

      {renovando && (
        <RenovarPertenenciaDialogo
          personaId={personaId}
          pertenencia={renovando}
          alCerrar={() => setRenovando(null)}
        />
      )}
    </section>
  )
}
