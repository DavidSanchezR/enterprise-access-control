import { useMutation } from '@tanstack/react-query'
import { useState, type ReactElement } from 'react'
import { ApiError } from '../../lib/apiClient'
import { aIsoUtc, aValorLocal } from '../../lib/fechas'
import { useAreas } from '../area-access/hooks'
import { useCompanias } from '../companies/hooks'
import { usePersonas } from '../people/hooks'
import { ETIQUETA_ALCANCE } from '../permissions/api'
import { evaluarAcceso } from './api'
import { EXPLICACION_MOTIVO, pasosDeEvaluacion, type EstadoPaso } from './pasos'
import './accessEvaluation.css'

const SIMBOLO: Record<EstadoPaso, string> = {
  CUMPLIDO: '✓',
  FALLIDO: '✗',
  NO_EVALUADO: '–',
}

const TEXTO_ESTADO: Record<EstadoPaso, string> = {
  CUMPLIDO: 'cumplido',
  FALLIDO: 'no cumplido',
  NO_EVALUADO: 'no evaluado',
}

/**
 * Prueba administrativa de evaluación de acceso (Historia 8; ux-ui.md §19).
 *
 * Responde "¿esta persona puede entrar a esta área en este momento?" y explica por qué. Es una
 * consulta: no modifica credenciales, contextos ni permisos, y una denegación es un resultado
 * legítimo, no un error.
 */
export function EvaluacionAccesoPage(): ReactElement {
  const [textoPersona, setTextoPersona] = useState('')
  const [personaId, setPersonaId] = useState('')
  const [principalId, setPrincipalId] = useState('')
  const [areaId, setAreaId] = useState('')
  const [fechaHora, setFechaHora] = useState(() => aValorLocal(new Date().toISOString()))

  const personas = usePersonas({ texto: textoPersona, tamañoPagina: 20 })
  const principales = useCompanias({
    tipoCompania: 'PRINCIPAL_MANDANTE',
    estado: 'ACTIVO',
    tamañoPagina: 200,
  })
  const areas = useAreas(principalId === '' ? null : principalId)

  const evaluacion = useMutation({ mutationFn: evaluarAcceso })

  const error = evaluacion.error instanceof ApiError ? evaluacion.error : undefined
  const resultado = evaluacion.data
  const listo = personaId !== '' && areaId !== '' && fechaHora !== ''

  const nombrePrincipal = (id: string | null): string =>
    principales.data?.items.find((c) => c.id === id)?.nombre ?? id ?? '—'

  return (
    <section className="evaluacion">
      <header>
        <h1>Evaluación de acceso</h1>
        <p className="evaluacion-subtitulo">
          Compruebe si una persona obtiene acceso a un área en una fecha y hora concretas, y qué
          validación lo determina.
        </p>
      </header>

      <form
        className="tarjeta evaluacion-formulario"
        onSubmit={(evento) => {
          evento.preventDefault()
          if (listo) {
            evaluacion.mutate({ personaId, areaAccesoId: areaId, fechaHora: aIsoUtc(fechaHora) })
          }
        }}
      >
        <div className="campo">
          <label htmlFor="evaluacion-buscar-persona">Buscar persona</label>
          <input
            id="evaluacion-buscar-persona"
            type="search"
            value={textoPersona}
            onChange={(evento) => setTextoPersona(evento.target.value)}
          />
        </div>

        <div className="campo">
          <label htmlFor="evaluacion-persona">Persona</label>
          <select
            id="evaluacion-persona"
            value={personaId}
            onChange={(evento) => setPersonaId(evento.target.value)}
          >
            <option value="">Seleccione una persona…</option>
            {(personas.data?.items ?? []).map((persona) => (
              <option key={persona.id} value={persona.id}>
                {persona.apellidos}, {persona.nombres} — {persona.numeroDocumento}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="evaluacion-principal">Compañía principal</label>
          <select
            id="evaluacion-principal"
            value={principalId}
            onChange={(evento) => {
              setPrincipalId(evento.target.value)
              setAreaId('')
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

        <div className="campo">
          <label htmlFor="evaluacion-area">Área</label>
          <select
            id="evaluacion-area"
            value={areaId}
            disabled={principalId === ''}
            onChange={(evento) => setAreaId(evento.target.value)}
          >
            <option value="">Seleccione un área…</option>
            {(areas.data ?? []).map((area) => (
              <option key={area.id} value={area.id}>
                {area.nombre}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="evaluacion-fecha">Fecha y hora</label>
          <input
            id="evaluacion-fecha"
            type="datetime-local"
            required
            value={fechaHora}
            onChange={(evento) => setFechaHora(evento.target.value)}
          />
        </div>

        <button type="submit" className="primario" disabled={!listo || evaluacion.isPending}>
          {evaluacion.isPending ? 'Evaluando…' : 'Evaluar acceso'}
        </button>
      </form>

      {error && (
        <p className="aviso error" role="alert">
          {error.status === 404
            ? 'Persona o área no encontrada, o fuera de su alcance.'
            : error.message}
        </p>
      )}

      {resultado && (
        <div className="tarjeta evaluacion-resultado" aria-live="polite">
          <p
            className={`evaluacion-veredicto ${resultado.resultado === 'CONCEDIDO' ? 'concedido' : 'denegado'}`}
          >
            <span aria-hidden="true">●</span>{' '}
            {resultado.resultado === 'CONCEDIDO' ? 'Acceso concedido' : 'Acceso denegado'}
          </p>

          <dl className="evaluacion-datos">
            <dt>Compañía principal evaluada</dt>
            <dd>{nombrePrincipal(resultado.companiaPrincipalId)}</dd>

            {resultado.motivoDenegacion && (
              <>
                <dt>Motivo</dt>
                <dd>
                  <code>{resultado.motivoDenegacion}</code>
                  <br />
                  {EXPLICACION_MOTIVO[resultado.motivoDenegacion]}
                </dd>
              </>
            )}

            {resultado.nivelAplicado && (
              <>
                <dt>Permiso aplicado</dt>
                <dd>
                  Nivel {ETIQUETA_ALCANCE[resultado.nivelAplicado]} ·{' '}
                  <code>{resultado.permisoAplicadoId}</code>
                </dd>
              </>
            )}

            <dt>Zona horaria de evaluación</dt>
            <dd>{resultado.evaluadoEnZonaHoraria}</dd>
          </dl>

          <h2>Validaciones</h2>
          <ol className="evaluacion-pasos">
            {pasosDeEvaluacion(resultado).map((paso) => (
              <li key={paso.clave} className={`paso-${paso.estado.toLowerCase()}`}>
                <span aria-hidden="true">{SIMBOLO[paso.estado]}</span> {paso.titulo}
                <span className="sr-only"> ({TEXTO_ESTADO[paso.estado]})</span>
              </li>
            ))}
          </ol>
        </div>
      )}
    </section>
  )
}
