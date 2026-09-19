import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { useMaestro } from '../masters/hooks'
import type { Persona, PersonaRequest } from './api'
import { useActualizarPersona, useCrearPersona } from './hooks'

/**
 * Los diez campos son obligatorios (RF-012). Un registro sin tipo de sangre o sin contacto de
 * emergencia es justamente el que falla cuando ocurre un accidente en faena, así que el formulario
 * no admite guardarlo a medias.
 */
const esquema = z.object({
  nombres: z.string().trim().min(1, 'Indique los nombres.').max(150, 'Máximo 150 caracteres.'),
  apellidos: z.string().trim().min(1, 'Indique los apellidos.').max(150, 'Máximo 150 caracteres.'),
  fechaNacimiento: z
    .string()
    .min(1, 'Indique la fecha de nacimiento.')
    .refine(
      (valor) => new Date(valor) < new Date(),
      'La fecha de nacimiento debe ser anterior a hoy.',
    ),
  tipoDocumentoId: z.string().min(1, 'Seleccione el tipo de documento.'),
  numeroDocumento: z
    .string()
    .trim()
    .min(1, 'Indique el número de documento.')
    .max(20, 'Máximo 20 caracteres.'),
  generoId: z.string().min(1, 'Seleccione el género.'),
  correoElectronico: z
    .string()
    .trim()
    .min(1, 'Indique el correo electrónico.')
    .max(256, 'Máximo 256 caracteres.')
    .email('El correo electrónico no tiene un formato válido.'),
  tipoSangreId: z.string().min(1, 'Seleccione el tipo de sangre.'),
  contactoEmergencia: z
    .string()
    .trim()
    .min(1, 'Indique el contacto de emergencia.')
    .max(150, 'Máximo 150 caracteres.'),
  numeroEmergencia: z
    .string()
    .trim()
    .min(1, 'Indique el número de emergencia.')
    .max(30, 'Máximo 30 caracteres.'),
})

type Formulario = z.infer<typeof esquema>

const VACIO: Formulario = {
  nombres: '',
  apellidos: '',
  fechaNacimiento: '',
  tipoDocumentoId: '',
  numeroDocumento: '',
  generoId: '',
  correoElectronico: '',
  tipoSangreId: '',
  contactoEmergencia: '',
  numeroEmergencia: '',
}

export function PersonaFormulario(
  props:
    | { modo: 'crear'; alCerrar: () => void }
    | { modo: 'editar'; persona: Persona; alCerrar: () => void },
): ReactElement {
  const { alCerrar } = props
  const persona = props.modo === 'editar' ? props.persona : null

  const crear = useCrearPersona()
  const actualizar = useActualizarPersona()

  // Solo se ofrecen valores ACTIVOS: uno INACTIVO no puede usarse en asignaciones nuevas (RF-032).
  const tiposDocumento = useMaestro('tipos-documento', 'ACTIVO')
  const generos = useMaestro('generos', 'ACTIVO')
  const tiposSangre = useMaestro('tipos-sangre', 'ACTIVO')

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquema),
    defaultValues: persona
      ? {
          nombres: persona.nombres,
          apellidos: persona.apellidos,
          fechaNacimiento: persona.fechaNacimiento,
          tipoDocumentoId: persona.tipoDocumentoId,
          numeroDocumento: persona.numeroDocumento,
          generoId: persona.generoId,
          correoElectronico: persona.correoElectronico,
          tipoSangreId: persona.tipoSangreId,
          contactoEmergencia: persona.contactoEmergencia,
          numeroEmergencia: persona.numeroEmergencia,
        }
      : VACIO,
  })

  const mutacion = persona ? actualizar : crear
  const error = mutacion.error instanceof ApiError ? mutacion.error : undefined

  function enviar(valores: Formulario): void {
    const entrada = valores satisfies PersonaRequest

    if (persona) {
      actualizar.mutate({ id: persona.id, ...entrada }, { onSuccess: alCerrar })
    } else {
      crear.mutate(entrada, { onSuccess: alCerrar })
    }
  }

  return (
    <Dialogo
      titulo={persona ? `Editar ${persona.nombres} ${persona.apellidos}` : 'Registrar persona'}
      alCerrar={alCerrar}
    >
      <form
        onSubmit={(evento) => {
          void handleSubmit(enviar)(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.codigo === 'DOCUMENTO_YA_REGISTRADO'
              ? 'Ya existe una persona con ese tipo y número de documento.'
              : error.message}
          </p>
        )}

        <div className="personas-rejilla">
          <div className="campo">
            <label htmlFor="persona-nombres">Nombres</label>
            <input
              id="persona-nombres"
              type="text"
              autoFocus
              aria-invalid={errors.nombres ? 'true' : undefined}
              {...register('nombres')}
            />
            {errors.nombres && (
              <span className="error-campo" role="alert">
                {errors.nombres.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-apellidos">Apellidos</label>
            <input
              id="persona-apellidos"
              type="text"
              aria-invalid={errors.apellidos ? 'true' : undefined}
              {...register('apellidos')}
            />
            {errors.apellidos && (
              <span className="error-campo" role="alert">
                {errors.apellidos.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-tipo-documento">Tipo de documento</label>
            <select
              id="persona-tipo-documento"
              aria-invalid={errors.tipoDocumentoId ? 'true' : undefined}
              {...register('tipoDocumentoId')}
            >
              <option value="">Seleccione…</option>
              {tiposDocumento.data?.map((tipo) => (
                <option key={tipo.id} value={tipo.id}>
                  {tipo.nombre}
                </option>
              ))}
            </select>
            {errors.tipoDocumentoId && (
              <span className="error-campo" role="alert">
                {errors.tipoDocumentoId.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-numero-documento">Número de documento</label>
            <input
              id="persona-numero-documento"
              type="text"
              maxLength={20}
              aria-invalid={errors.numeroDocumento ? 'true' : undefined}
              {...register('numeroDocumento')}
            />
            {errors.numeroDocumento && (
              <span className="error-campo" role="alert">
                {errors.numeroDocumento.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-fecha-nacimiento">Fecha de nacimiento</label>
            <input
              id="persona-fecha-nacimiento"
              type="date"
              aria-invalid={errors.fechaNacimiento ? 'true' : undefined}
              {...register('fechaNacimiento')}
            />
            {errors.fechaNacimiento && (
              <span className="error-campo" role="alert">
                {errors.fechaNacimiento.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-genero">Género</label>
            <select
              id="persona-genero"
              aria-invalid={errors.generoId ? 'true' : undefined}
              {...register('generoId')}
            >
              <option value="">Seleccione…</option>
              {generos.data?.map((genero) => (
                <option key={genero.id} value={genero.id}>
                  {genero.nombre}
                </option>
              ))}
            </select>
            {errors.generoId && (
              <span className="error-campo" role="alert">
                {errors.generoId.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-correo">Correo electrónico</label>
            <input
              id="persona-correo"
              type="email"
              aria-invalid={errors.correoElectronico ? 'true' : undefined}
              {...register('correoElectronico')}
            />
            {errors.correoElectronico && (
              <span className="error-campo" role="alert">
                {errors.correoElectronico.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-tipo-sangre">Tipo de sangre</label>
            <select
              id="persona-tipo-sangre"
              aria-invalid={errors.tipoSangreId ? 'true' : undefined}
              {...register('tipoSangreId')}
            >
              <option value="">Seleccione…</option>
              {tiposSangre.data?.map((tipo) => (
                <option key={tipo.id} value={tipo.id}>
                  {tipo.nombre}
                </option>
              ))}
            </select>
            {errors.tipoSangreId && (
              <span className="error-campo" role="alert">
                {errors.tipoSangreId.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-contacto">Contacto de emergencia</label>
            <input
              id="persona-contacto"
              type="text"
              aria-invalid={errors.contactoEmergencia ? 'true' : undefined}
              {...register('contactoEmergencia')}
            />
            {errors.contactoEmergencia && (
              <span className="error-campo" role="alert">
                {errors.contactoEmergencia.message}
              </span>
            )}
          </div>

          <div className="campo">
            <label htmlFor="persona-numero-emergencia">Número de emergencia</label>
            <input
              id="persona-numero-emergencia"
              type="tel"
              maxLength={30}
              aria-invalid={errors.numeroEmergencia ? 'true' : undefined}
              {...register('numeroEmergencia')}
            />
            {errors.numeroEmergencia && (
              <span className="error-campo" role="alert">
                {errors.numeroEmergencia.message}
              </span>
            )}
          </div>
        </div>

        <div className="dialogo-acciones">
          <button type="button" onClick={alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={mutacion.isPending}>
            {mutacion.isPending ? 'Guardando…' : persona ? 'Guardar cambios' : 'Registrar persona'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
