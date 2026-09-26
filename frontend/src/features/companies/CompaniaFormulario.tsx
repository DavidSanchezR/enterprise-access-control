import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, type ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { useMaestro } from '../masters/hooks'
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
  tipoDocumentoId: z.string().min(1, 'Seleccione el tipo de documento.'),
  numeroDocumento: z
    .string()
    .min(1, 'Indique el número de documento.')
    .max(20, 'Máximo 20 caracteres.'),
  // RF-042: la clasificación es obligatoria y no admite un valor "sin clasificar".
  tipoCompania: z.enum(TIPOS_COMPANIA),
  estado: z.enum(ESTADOS),
  // RF-080: identificador IANA propio de cada Compañía Principal.
  zonaHorariaIana: z.string(),
}).refine(
  (valores) =>
    valores.tipoCompania !== 'PRINCIPAL_MANDANTE' || valores.zonaHorariaIana.trim() !== '',
  {
    // Una Contratista no la necesita: no posee áreas ni bloques horarios propios (RF-080).
    message: 'Una compañía principal mandante debe declarar su zona horaria.',
    path: ['zonaHorariaIana'],
  },
)

type Formulario = z.infer<typeof esquema>

export function CompaniaFormulario(
  props:
    | { modo: 'crear'; alCerrar: () => void }
    | { modo: 'editar'; compania: Compania; alCerrar: () => void },
): ReactElement {
  const crear = useCrearCompania()
  const actualizar = useActualizarCompania()

  const edicion = props.modo === 'editar' ? props.compania : null

  // Se elige por nombre y viaja el id, que nunca se muestra (RF-013). Solo se ofrecen valores ACTIVOS:
  // uno INACTIVO no puede usarse en asignaciones nuevas (RF-032), igual que en el formulario de persona.
  const tiposDocumento = useMaestro('tipos-documento', 'ACTIVO')

  const {
    register,
    handleSubmit,
    getValues,
    setValue,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquema),
    defaultValues: {
      nombre: edicion?.nombre ?? '',
      tipoDocumentoId: edicion?.tipoDocumentoId ?? '',
      numeroDocumento: edicion?.numeroDocumento ?? '',
      tipoCompania: edicion?.tipoCompania ?? 'PRINCIPAL_MANDANTE',
      estado: edicion?.estado ?? 'ACTIVO',
      zonaHorariaIana: edicion?.zonaHorariaIana ?? 'America/Lima',
    },
  })

  // Las opciones llegan después del primer render, cuando el <select> ya no puede mostrar el valor
  // inicial. Al cargarlas se reaplica el tipo actual si está entre los ACTIVOS; si no lo está (inactivo o
  // no resoluble), se deja vacío para que haya que elegir uno activo y nunca se envíe un id oculto.
  useEffect(() => {
    if (!tiposDocumento.data) {
      return
    }

    const actual = getValues('tipoDocumentoId')
    setValue('tipoDocumentoId', tiposDocumento.data.some((t) => t.id === actual) ? actual : '')
  }, [tiposDocumento.data, getValues, setValue])

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
            const peticion = {
              ...valores,
              // Solo una Principal tiene zona propia; en una Contratista carece de uso funcional y
              // guardarla dejaría un dato que nada interpreta (RF-080).
              zonaHorariaIana:
                valores.tipoCompania !== 'PRINCIPAL_MANDANTE' ||
                valores.zonaHorariaIana.trim() === ''
                  ? null
                  : valores.zonaHorariaIana.trim(),
            }

            if (edicion) {
              actualizar.mutate({ id: edicion.id, ...peticion }, { onSuccess: props.alCerrar })
            } else {
              crear.mutate(peticion, { onSuccess: props.alCerrar })
            }
          })(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.codigo === 'DOCUMENTO_YA_REGISTRADO'
              ? 'Ya existe una compañía con ese tipo y número de documento.'
              : error.codigo === 'CAMBIO_TIPO_COMPANIA_CON_DEPENDENCIAS'
                ? `No se puede cambiar la clasificación mientras existan dependencias incompatibles. ${error.message}`
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
          <select
            id="compania-tipo-documento"
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
            Solo una compañía principal mandante puede poseer unidades organizativas. Cambiarla se
            rechaza si existen dependencias incompatibles con el tipo destino.
          </span>
        </div>

        <div className="campo">
          <label htmlFor="compania-zona">Zona horaria</label>
          <input
            id="compania-zona"
            type="text"
            list="zonas-iana"
            aria-invalid={errors.zonaHorariaIana ? 'true' : undefined}
            aria-describedby="compania-zona-ayuda"
            {...register('zonaHorariaIana')}
          />
          <datalist id="zonas-iana">
            <option value="America/Lima" />
            <option value="America/Santiago" />
            <option value="America/Bogota" />
            <option value="America/Mexico_City" />
          </datalist>
          <span className="campo-ayuda" id="compania-zona-ayuda">
            Identificador IANA. Interpreta los bloques horarios de sus áreas; los instantes se siguen
            almacenando en UTC, así que cambiarla no altera ningún registro anterior.
          </span>
          {errors.zonaHorariaIana && (
            <span className="error-campo" role="alert">
              {errors.zonaHorariaIana.message}
            </span>
          )}
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
