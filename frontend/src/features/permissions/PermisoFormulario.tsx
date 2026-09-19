import { zodResolver } from '@hookform/resolvers/zod'
import { useState, type ReactElement } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { aIsoUtc, aValorLocal } from '../../lib/fechas'
import { ESTADOS } from '../companies/api'
import { useCompanias } from '../companies/hooks'
import { useUnidades } from '../org-units/hooks'
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
    fechaHoraInicioVigencia: z.string().min(1, 'Indique el inicio de vigencia.'),
    fechaHoraFinVigencia: z.string().min(1, 'Indique el fin de vigencia: es obligatorio.'),
    estado: z.enum(ESTADOS),
  })
  .refine(
    (valores) =>
      valores.fechaHoraInicioVigencia === '' ||
      valores.fechaHoraFinVigencia === '' ||
      new Date(valores.fechaHoraFinVigencia) > new Date(valores.fechaHoraInicioVigencia),
    {
      path: ['fechaHoraFinVigencia'],
      message: 'El fin de vigencia debe ser posterior al inicio.',
    },
  )

type Formulario = z.infer<typeof esquema>

/**
 * Devuelve el instante original si el usuario no tocó el campo.
 *
 * `datetime-local` solo tiene precisión de minutos: reenviar un fin de vigencia `23:59:59.999` tal
 * como lo muestra el control lo convertiría en `23:59:00.000` y acortaría el permiso en silencio al
 * editar cualquier otro dato.
 */
function conservarSiNoCambio(valorLocal: string, original: string | undefined): string {
  return original !== undefined && valorLocal === aValorLocal(original)
    ? original
    : aIsoUtc(valorLocal)
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
      fechaHoraInicioVigencia: edicion ? aValorLocal(edicion.fechaHoraInicioVigencia) : '',
      fechaHoraFinVigencia: edicion ? aValorLocal(edicion.fechaHoraFinVigencia) : '',
      estado: edicion?.estado ?? 'ACTIVO',
    },
  })

  const alcance = useWatch({ control, name: 'alcance' })

  const personas = usePersonas({ texto: textoPersona, tamañoPagina: 20 })
  const unidades = useUnidades(props.companiaPrincipalId)
  // research.md §12: el alcance COMPAÑÍA admite Principales y Contratistas por igual.
  const companias = useCompanias({ estado: 'ACTIVO', tamañoPagina: 200 })

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
      fechaHoraInicioVigencia: conservarSiNoCambio(
        valores.fechaHoraInicioVigencia,
        edicion?.fechaHoraInicioVigencia,
      ),
      fechaHoraFinVigencia: conservarSiNoCambio(
        valores.fechaHoraFinVigencia,
        edicion?.fechaHoraFinVigencia,
      ),
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
            {error.message}
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

        <div className="permiso-vigencia">
          <div className="campo">
            <label htmlFor="permiso-inicio">Inicio de vigencia</label>
            <input
              id="permiso-inicio"
              type="datetime-local"
              required
              aria-invalid={errors.fechaHoraInicioVigencia ? 'true' : undefined}
              {...register('fechaHoraInicioVigencia')}
            />
            {errors.fechaHoraInicioVigencia && (
              <span className="error-campo" role="alert">
                {errors.fechaHoraInicioVigencia.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="permiso-fin">Fin de vigencia</label>
            <input
              id="permiso-fin"
              type="datetime-local"
              required
              aria-invalid={errors.fechaHoraFinVigencia ? 'true' : undefined}
              aria-describedby="permiso-fin-ayuda"
              {...register('fechaHoraFinVigencia')}
            />
            <span className="campo-ayuda" id="permiso-fin-ayuda">
              Obligatorio: un permiso de acceso siempre tiene fecha de término.
            </span>
            {errors.fechaHoraFinVigencia && (
              <span className="error-campo" role="alert">
                {errors.fechaHoraFinVigencia.message}
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
