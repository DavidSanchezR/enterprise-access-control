import { useMemo, useState, type ReactElement } from 'react'
import { Dialogo } from '../../../components/Dialogo'
import { ApiError } from '../../../lib/apiClient'
import { useCompanias, useRelaciones } from '../../companies/hooks'
import { useAbrirContexto, useCrearPertenencia } from './hooks'

/**
 * Alta de pertenencia y apertura del contexto operativo, en un solo flujo (decisión D7, RF-053,
 * RF-054).
 *
 * **Caso A — la persona pertenece a una Compañía Principal**: su contexto operativo queda fijado
 * automáticamente a esa misma Principal (RF-053). No se ofrece elegirla, porque no hay nada que
 * elegir: proponerlo sugeriría una libertad que el dominio no concede.
 *
 * **Caso B — la persona pertenece a una Contratista**: el contexto se abre con una Principal con la
 * que esa Contratista mantenga una `RelaciónContratistaPrincipal` vigente (RF-054). El selector se
 * limita a esas relaciones, de modo que la interfaz no ofrezca una Principal que el servidor
 * rechazaría.
 *
 * Hasta esta sesión, `useCrearPertenencia` y `useAbrirContexto` existían y funcionaban pero ninguna
 * pantalla los invocaba: Historia 5 solo era operable por API.
 */
export function PertenenciaContextoWizard({
  personaId,
  alCerrar,
}: {
  personaId: string
  alCerrar: () => void
}): ReactElement {
  const companias = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

  const [companiaId, setCompaniaId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [principalId, setPrincipalId] = useState('')
  const [abrirTambienContexto, setAbrirTambienContexto] = useState(true)

  const crearPertenencia = useCrearPertenencia(personaId)
  const abrirContexto = useAbrirContexto(personaId)

  const compania = useMemo(
    () => companias.data?.items.find((c) => c.id === companiaId),
    [companias.data, companiaId],
  )

  const esContratista = compania?.tipoCompania === 'CONTRATISTA'

  // Caso B: solo las Principales con relación vigente con esa Contratista (RF-054).
  const relaciones = useRelaciones(esContratista ? companiaId : null)

  const ahora = Date.now()

  const principalesDisponibles = useMemo(() => {
    if (!esContratista) {
      return []
    }

    const vigentes = (relaciones.data ?? []).filter(
      (r) =>
        new Date(r.fechaHoraInicio).getTime() <= ahora &&
        (r.fechaHoraFin === null || ahora < new Date(r.fechaHoraFin).getTime()),
    )

    return vigentes.map((r) => ({
      id: r.companiaPrincipalId,
      nombre:
        companias.data?.items.find((c) => c.id === r.companiaPrincipalId)?.nombre ??
        'Compañía principal fuera de su alcance',
    }))
  }, [esContratista, relaciones.data, companias.data, ahora])

  // Caso A: la Principal es la propia compañía de pertenencia; no se elige (RF-053).
  const principalEfectiva = esContratista ? principalId : companiaId

  const error =
    (crearPertenencia.error instanceof ApiError && crearPertenencia.error) ||
    (abrirContexto.error instanceof ApiError && abrirContexto.error) ||
    undefined

  const completo =
    companiaId !== '' &&
    desde !== '' &&
    hasta !== '' &&
    hasta > desde &&
    (!abrirTambienContexto || principalEfectiva !== '')

  const enCurso = crearPertenencia.isPending || abrirContexto.isPending

  function enviar(): void {
    const fechaHoraInicio = new Date(`${desde}T00:00:00Z`).toISOString()
    const fechaHoraFin = new Date(`${hasta}T00:00:00Z`).toISOString()

    crearPertenencia.mutate(
      { companiaId, fechaHoraInicio, fechaHoraFin },
      {
        onSuccess: () => {
          if (!abrirTambienContexto) {
            alCerrar()
            return
          }

          // El contexto se abre después de la pertenencia: sin pertenencia vigente el servidor
          // rechazaría la apertura, y el orden importa (RF-053, RF-054).
          abrirContexto.mutate(
            { companiaPrincipalId: principalEfectiva, fechaHoraInicio, fechaHoraFin },
            { onSuccess: alCerrar },
          )
        },
      },
    )
  }

  return (
    <Dialogo titulo="Registrar pertenencia y contexto" alCerrar={alCerrar}>
      {error && (
        <p className="aviso error" role="alert">
          {error.message}
        </p>
      )}

      <div className="campo">
        <label htmlFor="pertenencia-compania">Compañía de pertenencia</label>
        <select
          id="pertenencia-compania"
          value={companiaId}
          onChange={(evento) => {
            setCompaniaId(evento.target.value)
            setPrincipalId('')
          }}
        >
          <option value="">Seleccione…</option>
          {(companias.data?.items ?? []).map((c) => (
            <option key={c.id} value={c.id}>
              {c.nombre}
            </option>
          ))}
        </select>
        <span className="campo-ayuda">
          Puede ser principal mandante o contratista: es la relación laboral o contractual.
        </span>
      </div>

      <div className="campo">
        <label htmlFor="pertenencia-desde">Desde</label>
        <input
          id="pertenencia-desde"
          type="date"
          value={desde}
          onChange={(evento) => setDesde(evento.target.value)}
        />
      </div>

      <div className="campo">
        <label htmlFor="pertenencia-hasta">Hasta</label>
        <input
          id="pertenencia-hasta"
          type="date"
          value={hasta}
          onChange={(evento) => setHasta(evento.target.value)}
        />
        {/* RF-071: no existe pertenencia de vigencia indefinida. */}
        <span className="campo-ayuda">Obligatorio: toda pertenencia tiene fecha de fin.</span>
      </div>

      <div className="campo filtro-casilla">
        <label htmlFor="pertenencia-contexto">
          <input
            id="pertenencia-contexto"
            type="checkbox"
            checked={abrirTambienContexto}
            onChange={(evento) => setAbrirTambienContexto(evento.target.checked)}
          />
          Abrir también su contexto operativo
        </label>
      </div>

      {abrirTambienContexto && companiaId !== '' && !esContratista && (
        <p className="aviso info">
          La persona pertenece a una compañía principal, así que su contexto operativo se abre
          automáticamente con esa misma principal.
        </p>
      )}

      {abrirTambienContexto && esContratista && (
        <div className="campo">
          <label htmlFor="pertenencia-principal">Compañía principal del contexto</label>
          <select
            id="pertenencia-principal"
            value={principalId}
            onChange={(evento) => setPrincipalId(evento.target.value)}
          >
            <option value="">Seleccione…</option>
            {principalesDisponibles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.nombre}
              </option>
            ))}
          </select>

          {principalesDisponibles.length === 0 && !relaciones.isPending && (
            <span className="campo-ayuda">
              Esa contratista no tiene relaciones vigentes con ninguna compañía principal de su
              alcance.
            </span>
          )}
        </div>
      )}

      <div className="dialogo-acciones">
        <button type="button" onClick={alCerrar}>
          Cancelar
        </button>
        <button type="button" className="primario" disabled={!completo || enCurso} onClick={enviar}>
          {enCurso ? 'Registrando…' : 'Registrar'}
        </button>
      </div>
    </Dialogo>
  )
}
