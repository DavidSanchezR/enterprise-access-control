import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { useState, type ReactElement } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { fechaDeclarada, formatearFecha, formatearFechaHora } from '../../lib/fechas'
import { CodigosError } from '../../lib/problemDetails'
import { ESTADOS } from '../companies/api'
import { useCompanias } from '../companies/hooks'
import { useUnidades } from '../org-units/hooks'
import { listarHistorialCompanias } from '../people/history/api'
import { clavesHistorial } from '../people/history/hooks'
import { pertenenciaVigente } from '../people/history/pertenenciaVigente'
import { usePersonas } from '../people/hooks'
import {
  ALCANCES,
  ETIQUETA_ALCANCE,
  sujetoDe,
  type AlcancePermiso,
  type BloqueHorario,
  type PermisoAcceso,
} from './api'
import { hayErroresEnBloques } from './bloques'
import { useActualizarPermiso, useCrearPermiso } from './hooks'
import { SelectorSemanal } from './SelectorSemanal'

const esquema = z
  .object({
    alcance: z.enum(ALCANCES),
    sujetoId: z.string().min(1, 'Indique a quién se otorga el permiso.'),
    // RF-021, RF-071: inicio y fin obligatorios para los tres alcances; no hay vigencia indefinida.
    // VF-004 (RF-083): son fechas civiles `AAAA-MM-DD`, cada una un día completo en la zona de la Principal.
    fechaInicioVigencia: z.string().min(1, 'Indique el inicio de vigencia.'),
    fechaFinVigencia: z.string().min(1, 'Indique el fin de vigencia: es obligatorio.'),
    estado: z.enum(ESTADOS),
  })
  .refine(
    // `AAAA-MM-DD` se ordena igual como texto que como fecha. La igualdad es válida: un permiso de un día.
    (valores) =>
      valores.fechaInicioVigencia === '' ||
      valores.fechaFinVigencia === '' ||
      valores.fechaFinVigencia >= valores.fechaInicioVigencia,
    {
      path: ['fechaFinVigencia'],
      message: 'El fin de vigencia no puede ser anterior al inicio.',
    },
  )

type Formulario = z.infer<typeof esquema>

/**
 * Mensaje para los rechazos de contención de RF-082 (cambio post-Baseline VF-007), que solo se dan en
 * permisos por persona. El resto de errores conserva el texto del servidor.
 */
function mensajeDeError(error: ApiError): string {
  switch (error.codigo) {
    case CodigosError.FUERA_DE_CONTENCION_TEMPORAL:
      return 'La vigencia de un permiso por persona debe quedar dentro de la pertenencia vigente de esa persona.'
    case CodigosError.SIN_PERTENENCIA_VIGENTE:
      return 'La persona no tiene una pertenencia vigente: no se le puede otorgar, ampliar ni reactivar un permiso por persona.'
    default:
      return error.message
  }
}

/**
 * Alta y edición de un permiso de acceso con su horario semanal (Historia 8, RF-020 a RF-022).
 *
 * Al editar, el alcance y el sujeto quedan fijos: el servidor no los admite cambiados, porque
 * reasignar un permiso ya otorgado equivaldría a otorgar uno nuevo a otra persona conservando el
 * rastro del anterior.
 */
export function PermisoFormulario(
  props: {
    areaAccesoId: string
    nombreArea: string
    companiaPrincipalId: string
    alCerrar: () => void
  } & ({ modo: 'crear' } | { modo: 'editar'; permiso: PermisoAcceso }),
): ReactElement {
  const edicion = props.modo === 'editar' ? props.permiso : null

  const crear = useCrearPermiso()
  const actualizar = useActualizarPermiso()
  const mutacion = edicion ? actualizar : crear

  const [bloques, setBloques] = useState<Omit<BloqueHorario, 'id'>[]>(
    edicion?.bloquesHorarios.map(({ diaSemana, horaInicio, horaFin }) => ({
      diaSemana,
      horaInicio,
      horaFin,
    })) ?? [],
  )
  const [intentoEnvio, setIntentoEnvio] = useState(false)
  const [textoPersona, setTextoPersona] = useState('')

  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquema),
    defaultValues: {
      alcance: edicion?.alcance ?? 'PERSONA',
      sujetoId: edicion ? (sujetoDe(edicion) ?? '') : '',
      // Fechas civiles calculadas por el servidor en la zona de la Principal (RF-083): se reenvían tal cual y,
      // si no cambian, el servidor conserva el instante almacenado (F-6).
      fechaInicioVigencia: edicion?.fechaInicioVigencia ?? '',
      fechaFinVigencia: edicion?.fechaFinVigencia ?? '',
      estado: edicion?.estado ?? 'ACTIVO',
    },
  })

  const alcance = useWatch({ control, name: 'alcance' })
  const sujetoId = useWatch({ control, name: 'sujetoId' })

  const personas = usePersonas({ texto: textoPersona, tamañoPagina: 20 })
  const unidades = useUnidades(props.companiaPrincipalId)
  // research.md §12: el alcance COMPAÑÍA admite Principales y Contratistas por igual.
  const companias = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

  // RF-082 (VF-007): orientación, nunca regla. Solo el alcance PERSONA tiene una pertenencia que sirva
  // de límite. Si el historial no puede leerse —p. ej. 404, porque se administra el área pero no la
  // persona—, el formulario sigue funcionando sin límite y decide el servidor (research.md §35.5).
  const personaId = alcance === 'PERSONA' ? sujetoId : ''
  const historial = useQuery({
    queryKey: clavesHistorial.companias(personaId),
    queryFn: () => listarHistorialCompanias(personaId),
    enabled: personaId !== '',
    retry: false,
  })
  const vigente = personaId === '' ? undefined : pertenenciaVigente(historial.data)

  // VF-004: la contención del permiso se compara por fecha civil (RF-083 (c)), así que el límite son las
  // fechas que declara la pertenencia, sin convertirlas a ninguna zona (research.md §36.7).
  const minimo = vigente ? fechaDeclarada(vigente.fechaHoraInicio) : undefined
  const maximo = vigente ? fechaDeclarada(vigente.fechaHoraFin) : undefined

  const error = mutacion.error instanceof ApiError ? mutacion.error : undefined

  function enviar(valores: Formulario): void {
    setIntentoEnvio(true)

    if (hayErroresEnBloques(bloques)) {
      return
    }

    const cuerpo = {
      areaAccesoId: props.areaAccesoId,
      alcance: valores.alcance,
      personaId: valores.alcance === 'PERSONA' ? valores.sujetoId : null,
      unidadOrganizativaId: valores.alcance === 'UNIDAD_ORGANIZATIVA' ? valores.sujetoId : null,
      companiaId: valores.alcance === 'COMPANIA' ? valores.sujetoId : null,
      fechaInicioVigencia: valores.fechaInicioVigencia,
      fechaFinVigencia: valores.fechaFinVigencia,
      estado: valores.estado,
      bloquesHorarios: bloques,
    }

    if (edicion) {
      actualizar.mutate({ id: edicion.id, ...cuerpo }, { onSuccess: props.alCerrar })
    } else {
      crear.mutate(cuerpo, { onSuccess: props.alCerrar })
    }
  }

  return (
    <Dialogo
      titulo={
        edicion ? `Editar permiso en ${props.nombreArea}` : `Nuevo permiso en ${props.nombreArea}`
      }
      alCerrar={props.alCerrar}
    >
      <form
        onSubmit={(evento) => {
          setIntentoEnvio(true)
          void handleSubmit(enviar)(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {mensajeDeError(error)}
          </p>
        )}

        {edicion ? (
          // Solo lectura: los valores viajan desde defaultValues, no desde controles deshabilitados.
          <p className="campo-ayuda">
            Otorgado a <strong>{ETIQUETA_ALCANCE[edicion.alcance]}</strong>. El alcance y el sujeto
            de un permiso ya otorgado no pueden cambiarse.
          </p>
        ) : (
          <>
            <fieldset className="campo">
              <legend>Otorgado a</legend>
              <div className="permiso-alcances">
                {ALCANCES.map((valor: AlcancePermiso) => (
                  <label key={valor}>
                    <input type="radio" value={valor} {...register('alcance')} />
                    {ETIQUETA_ALCANCE[valor]}
                  </label>
                ))}
              </div>
            </fieldset>

            {alcance === 'PERSONA' && (
              <div className="campo">
                <label htmlFor="permiso-buscar-persona">Buscar persona</label>
                <input
                  id="permiso-buscar-persona"
                  type="search"
                  placeholder="Ingrese su nro. de documento"
                  value={textoPersona}
                  onChange={(evento) => setTextoPersona(evento.target.value)}
                />
              </div>
            )}

            <div className="campo">
              <label htmlFor="permiso-sujeto">{ETIQUETA_ALCANCE[alcance]}</label>
              <select
                id="permiso-sujeto"
                aria-invalid={errors.sujetoId ? 'true' : undefined}
                {...register('sujetoId')}
              >
                <option value="">Seleccione…</option>
                {alcance === 'PERSONA' &&
                  (personas.data?.items ?? []).map((persona) => (
                    <option key={persona.id} value={persona.id}>
                      {persona.apellidos}, {persona.nombres} — {persona.numeroDocumento}
                    </option>
                  ))}
                {alcance === 'UNIDAD_ORGANIZATIVA' &&
                  (unidades.data ?? []).map((unidad) => (
                    <option key={unidad.id} value={unidad.id}>
                      {unidad.nombre}
                    </option>
                  ))}
                {alcance === 'COMPANIA' &&
                  (companias.data?.items ?? []).map((compania) => (
                    <option key={compania.id} value={compania.id}>
                      {compania.nombre}
                    </option>
                  ))}
              </select>
              {errors.sujetoId && (
                <span className="error-campo" role="alert">
                  {errors.sujetoId.message}
                </span>
              )}
            </div>
          </>
        )}

        {edicion && !edicion.vigenciaEnDiasCompletos && (
          // F-5/F-6: un permiso anterior con hora no se presenta como si fuera de días completos.
          <p className="aviso" role="note">
            Este permiso tiene una vigencia con hora:{' '}
            {formatearFechaHora(edicion.fechaHoraInicioVigencia, edicion.zonaHorariaIana)} –{' '}
            {formatearFechaHora(edicion.fechaHoraFinVigencia, edicion.zonaHorariaIana)}. Si conserva
            una fecha, se conserva también su hora; solo cambia el extremo cuya fecha modifique.
          </p>
        )}

        <div className="permiso-vigencia">
          <div className="campo">
            <label htmlFor="permiso-inicio">Inicio de vigencia</label>
            <input
              id="permiso-inicio"
              type="date"
              required
              min={minimo}
              max={maximo}
              aria-invalid={errors.fechaInicioVigencia ? 'true' : undefined}
              {...register('fechaInicioVigencia')}
            />
            {errors.fechaInicioVigencia && (
              <span className="error-campo" role="alert">
                {errors.fechaInicioVigencia.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="permiso-fin">Fin de vigencia</label>
            <input
              id="permiso-fin"
              type="date"
              required
              min={minimo}
              max={maximo}
              aria-invalid={errors.fechaFinVigencia ? 'true' : undefined}
              aria-describedby="permiso-fin-ayuda"
              {...register('fechaFinVigencia')}
            />
            <span className="campo-ayuda" id="permiso-fin-ayuda">
              Obligatorio: un permiso de acceso siempre tiene fecha de término. Cada fecha es un día
              completo; los horarios dentro del día se definen con los bloques horarios.
              {vigente &&
                ` Por persona, debe quedar dentro de su pertenencia vigente (${formatearFecha(
                  fechaDeclarada(vigente.fechaHoraInicio),
                )} – ${formatearFecha(fechaDeclarada(vigente.fechaHoraFin))}).`}
            </span>
            {errors.fechaFinVigencia && (
              <span className="error-campo" role="alert">
                {errors.fechaFinVigencia.message}
              </span>
            )}
          </div>
        </div>

        <div className="campo">
          <label htmlFor="permiso-estado">Estado</label>
          <select id="permiso-estado" {...register('estado')}>
            <option value="ACTIVO">Activo</option>
            <option value="INACTIVO">Inactivo (no concede acceso)</option>
          </select>
        </div>

        <SelectorSemanal bloques={bloques} onChange={setBloques} />

        {intentoEnvio && hayErroresEnBloques(bloques) && (
          <p className="aviso error" role="alert">
            Revise los bloques horarios antes de guardar.
          </p>
        )}

        <div className="dialogo-acciones">
          <button type="button" onClick={props.alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={mutacion.isPending}>
            {mutacion.isPending ? 'Guardando…' : 'Guardar permiso'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
