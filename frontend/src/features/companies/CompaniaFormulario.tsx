import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import {
  ESTADOS,
  ETIQUETA_TIPO_COMPANIA,
  TIPOS_COMPANIA,
  type Compania,
  type Estado,
  type TipoCompania,
} from './api'
import { useActualizarCompania, useCrearCompania } from './hooks'

const esquema = z.object({
  nombre: z.string().min(1, 'Indique el nombre.').max(200, 'Máximo 200 caracteres.'),
  tipoDocumentoId: z.string().uuid('Indique un identificador de tipo de documento válido.'),
  numeroDocumento: z
    .string()
    .min(1, 'Indique el número de documento.')
    .max(20, 'Máximo 20 caracteres.'),
  // RF-042: la clasificación es obligatoria y no admite un valor "sin clasificar".
  tipoCompania: z.enum(TIPOS_COMPANIA),
  estado: z.enum(ESTADOS),
})

type Formulario = z.infer<typeof esquema>

export function CompaniaFormulario(
  props:
    | { modo: 'crear'; alCerrar: () => void }
    | { modo: 'editar'; compania: Compania; alCerrar: () => void },
): ReactElement {
  const crear = useCrearCompania()
  const actualizar = useActualizarCompania()

  const edicion = props.modo === 'editar' ? props.compania : null

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquema),
    defaultValues: {
      nombre: edicion?.nombre ?? '',
      tipoDocumentoId: edicion?.tipoDocumentoId ?? '',
      numeroDocumento: edicion?.numeroDocumento ?? '',
      tipoCompania: edicion?.tipoCompania ?? 'PRINCIPAL_MANDANTE',
      estado: edicion?.estado ?? 'ACTIVO',
    },
  })

  const mutacion = edicion ? actualizar : crear
  const error = mutacion.error instanceof ApiError ? mutacion.error : undefined

  return (
    <Dialogo
      titulo={edicion ? `Editar ${edicion.nombre}` : 'Nueva compañía'}
      alCerrar={props.alCerrar}
    >
      <form
        onSubmit={(evento) => {
          void handleSubmit((valores) => {
            if (edicion) {
              actualizar.mutate({ id: edicion.id, ...valores }, { onSuccess: props.alCerrar })
            } else {
              crear.mutate(valores, { onSuccess: props.alCerrar })
            }
          })(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.codigo === 'DOCUMENTO_YA_REGISTRADO'
              ? 'Ya existe una compañía con ese tipo y número de documento.'
              : error.message}
          </p>
        )}

        <div className="campo">
          <label htmlFor="compania-nombre">Nombre</label>
          <input
            id="compania-nombre"
            type="text"
            autoFocus
            aria-invalid={errors.nombre ? 'true' : undefined}
            {...register('nombre')}
          />
          {errors.nombre && (
            <span className="error-campo" role="alert">
              {errors.nombre.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="compania-tipo-documento">Tipo de documento</label>
          <input
            id="compania-tipo-documento"
            type="text"
            aria-invalid={errors.tipoDocumentoId ? 'true' : undefined}
            aria-describedby="compania-tipo-documento-ayuda"
            {...register('tipoDocumentoId')}
          />
          <span className="campo-ayuda" id="compania-tipo-documento-ayuda">
            Identificador del maestro de tipos de documento.
          </span>
          {errors.tipoDocumentoId && (
            <span className="error-campo" role="alert">
              {errors.tipoDocumentoId.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="compania-numero-documento">Número de documento</label>
          <input
            id="compania-numero-documento"
            type="text"
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
          <label htmlFor="compania-tipo">Clasificación</label>
          <select id="compania-tipo" {...register('tipoCompania')}>
            {TIPOS_COMPANIA.map((valor: TipoCompania) => (
              <option key={valor} value={valor}>
                {ETIQUETA_TIPO_COMPANIA[valor]}
              </option>
            ))}
          </select>
          <span className="campo-ayuda">
            Solo una compañía principal mandante puede poseer unidades organizativas.
          </span>
        </div>

        <div className="campo">
          <label htmlFor="compania-estado">Estado</label>
          <select id="compania-estado" {...register('estado')}>
            {ESTADOS.map((valor: Estado) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
        </div>

        <div className="dialogo-acciones">
          <button type="button" onClick={props.alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={mutacion.isPending}>
            {mutacion.isPending ? 'Guardando…' : edicion ? 'Guardar cambios' : 'Crear compañía'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
