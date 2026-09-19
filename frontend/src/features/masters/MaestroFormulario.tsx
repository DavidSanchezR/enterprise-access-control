import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { ESTADOS } from '../companies/api'
import type { Catalogo, MasterItem } from './api'
import { useActualizarMaestro, useCrearMaestro } from './hooks'

/**
 * El esquema se construye por catálogo porque la longitud máxima del nombre cambia entre ellos
 * (10 caracteres en tipo de sangre, 100 en los demás). Validar con el límite real evita que el
 * usuario escriba un valor que el servidor rechazaría.
 */
function esquemaDe(catalogo: Catalogo) {
  return z.object({
    nombre: z
      .string()
      .trim()
      .min(1, 'Indique el nombre.')
      .max(catalogo.limiteNombre, `Máximo ${catalogo.limiteNombre} caracteres.`),
    estado: z.enum(ESTADOS),
  })
}

type Formulario = { nombre: string; estado: (typeof ESTADOS)[number] }

export function MaestroFormulario(
  props:
    | { modo: 'crear'; catalogo: Catalogo; alCerrar: () => void }
    | { modo: 'editar'; catalogo: Catalogo; item: MasterItem; alCerrar: () => void },
): ReactElement {
  const { catalogo, alCerrar } = props
  const item = props.modo === 'editar' ? props.item : null

  const crear = useCrearMaestro(catalogo.ruta)
  const actualizar = useActualizarMaestro(catalogo.ruta)

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Formulario>({
    resolver: zodResolver(esquemaDe(catalogo)),
    defaultValues: { nombre: item?.nombre ?? '', estado: item?.estado ?? 'ACTIVO' },
  })

  const mutacion = item ? actualizar : crear
  const error = mutacion.error instanceof ApiError ? mutacion.error : undefined

  return (
    <Dialogo
      titulo={item ? `Editar ${item.nombre}` : `Nuevo valor en ${catalogo.titulo.toLowerCase()}`}
      alCerrar={alCerrar}
    >
      <form
        onSubmit={(evento) => {
          void handleSubmit((valores) => {
            if (item) {
              actualizar.mutate({ id: item.id, ...valores }, { onSuccess: alCerrar })
            } else {
              crear.mutate(valores, { onSuccess: alCerrar })
            }
          })(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.codigo === 'NOMBRE_YA_REGISTRADO'
              ? 'Ya existe un valor con ese nombre en este catálogo.'
              : error.message}
          </p>
        )}

        <div className="campo">
          <label htmlFor="maestro-nombre">Nombre</label>
          <input
            id="maestro-nombre"
            type="text"
            autoFocus
            maxLength={catalogo.limiteNombre}
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
          <label htmlFor="maestro-estado">Estado</label>
          <select id="maestro-estado" {...register('estado')}>
            {ESTADOS.map((valor) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
          <span className="campo-ayuda">
            Un valor INACTIVO deja de ofrecerse en asignaciones nuevas, pero sigue siendo legible en
            el histórico.
          </span>
        </div>

        <div className="dialogo-acciones">
          <button type="button" onClick={alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={mutacion.isPending}>
            {mutacion.isPending ? 'Guardando…' : item ? 'Guardar cambios' : 'Crear valor'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
