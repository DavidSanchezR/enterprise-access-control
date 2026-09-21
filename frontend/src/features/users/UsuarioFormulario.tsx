import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ESTADOS_USUARIO, type EstadoUsuario, type Usuario } from './api'
import { useActualizarUsuario } from './hooks'
import { describirError } from './mensajesRol'

const esquemaEditar = z.object({
  correo: z.string().min(1, 'Indique el correo.').email('El correo no tiene un formato válido.'),
  estado: z.enum(ESTADOS_USUARIO),
})

type FormularioEditar = z.infer<typeof esquemaEditar>

/**
 * Edición de los datos propios del usuario: correo y estado.
 *
 * **No toca el rol ni el alcance** (contracts/users.yaml, PUT /api/usuarios/{id}): las asignaciones
 * tienen vigencia propia y se administran desde el detalle, agregándolas y finalizándolas una a una.
 * El alta, en cambio, es un wizard aparte porque un usuario nace con su primera asignación (RF-074).
 */
export function UsuarioFormulario({
  usuario,
  alCerrar,
}: {
  usuario: Usuario
  alCerrar: () => void
}): ReactElement {
  const actualizar = useActualizarUsuario()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormularioEditar>({
    resolver: zodResolver(esquemaEditar),
    defaultValues: { correo: usuario.correo, estado: usuario.estado },
  })

  const error = describirError(actualizar.error)

  return (
    <Dialogo titulo={`Editar ${usuario.correo}`} alCerrar={alCerrar}>
      <form
        onSubmit={(evento) => {
          void handleSubmit((valores) => {
            actualizar.mutate(
              { id: usuario.id, correo: valores.correo, estado: valores.estado as EstadoUsuario },
              { onSuccess: alCerrar },
            )
          })(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.mensaje}
          </p>
        )}

        <div className="campo">
          <label htmlFor="editar-correo">Correo</label>
          <input
            id="editar-correo"
            type="email"
            autoFocus
            aria-invalid={errors.correo ? 'true' : undefined}
            {...register('correo')}
          />
          {errors.correo && (
            <span className="error-campo" role="alert">
              {errors.correo.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="editar-estado">Estado</label>
          <select id="editar-estado" {...register('estado')}>
            {ESTADOS_USUARIO.map((valor) => (
              <option key={valor} value={valor}>
                {valor}
              </option>
            ))}
          </select>
          <span className="campo-ayuda">
            Volver a ACTIVO también reinicia el contador de intentos fallidos.
          </span>
        </div>

        <div className="dialogo-acciones">
          <button type="button" onClick={alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={actualizar.isPending}>
            {actualizar.isPending ? 'Guardando…' : 'Guardar cambios'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}
