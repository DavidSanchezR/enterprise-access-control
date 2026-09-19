import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Dialogo } from '../../components/Dialogo'
import { ApiError } from '../../lib/apiClient'
import { ESTADOS_USUARIO, type EstadoUsuario, type Usuario } from './api'
import { useActualizarUsuario, useCrearUsuario } from './hooks'

const esquemaBase = {
  correo: z.string().min(1, 'Indique el correo.').email('El correo no tiene un formato válido.'),
}

const esquemaCrear = z.object({
  ...esquemaBase,
  // La complejidad la valida el servidor, única fuente de la política vigente (Decisiones
  // Pendientes #1): aquí sólo se exige que el campo venga relleno.
  passwordInicial: z.string().min(1, 'Indique la contraseña inicial.'),
  // contracts/users.yaml declara minItems: 1 para companiaIds.
  companiaIds: z.string().min(1, 'Indique al menos una compañía.'),
})

const esquemaEditar = z.object({
  ...esquemaBase,
  estado: z.enum(ESTADOS_USUARIO),
})

type FormularioCrear = z.infer<typeof esquemaCrear>
type FormularioEditar = z.infer<typeof esquemaEditar>

/** Acepta identificadores separados por coma, espacio o salto de línea. */
function separarIds(entrada: string): string[] {
  return entrada
    .split(/[\s,;]+/)
    .map((valor) => valor.trim())
    .filter((valor) => valor.length > 0)
}

export function UsuarioFormulario(
  props:
    | { modo: 'crear'; alCerrar: () => void }
    | { modo: 'editar'; usuario: Usuario; alCerrar: () => void },
): ReactElement {
  return props.modo === 'crear' ? (
    <FormularioCrearUsuario alCerrar={props.alCerrar} />
  ) : (
    <FormularioEditarUsuario usuario={props.usuario} alCerrar={props.alCerrar} />
  )
}

function FormularioCrearUsuario({ alCerrar }: { alCerrar: () => void }): ReactElement {
  const crear = useCrearUsuario()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormularioCrear>({
    resolver: zodResolver(esquemaCrear),
    defaultValues: { correo: '', passwordInicial: '', companiaIds: '' },
  })

  const error = crear.error instanceof ApiError ? crear.error : undefined

  return (
    <Dialogo titulo="Nuevo usuario" alCerrar={alCerrar}>
      <form
        onSubmit={(evento) => {
          void handleSubmit((valores) => {
            crear.mutate(
              {
                correo: valores.correo,
                passwordInicial: valores.passwordInicial,
                companiaIds: separarIds(valores.companiaIds),
              },
              { onSuccess: alCerrar },
            )
          })(evento)
        }}
        noValidate
      >
        {error && (
          <p className="aviso error" role="alert">
            {error.message}
          </p>
        )}

        <div className="campo">
          <label htmlFor="crear-correo">Correo</label>
          <input
            id="crear-correo"
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
          <label htmlFor="crear-password">Contraseña inicial</label>
          <input
            id="crear-password"
            type="password"
            autoComplete="new-password"
            aria-invalid={errors.passwordInicial ? 'true' : undefined}
            aria-describedby="crear-password-ayuda"
            {...register('passwordInicial')}
          />
          <span className="campo-ayuda" id="crear-password-ayuda">
            El titular deberá cambiarla en su primer acceso.
          </span>
          {errors.passwordInicial && (
            <span className="error-campo" role="alert">
              {errors.passwordInicial.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="crear-companias">Compañías administrables</label>
          <input
            id="crear-companias"
            type="text"
            aria-invalid={errors.companiaIds ? 'true' : undefined}
            aria-describedby="crear-companias-ayuda"
            {...register('companiaIds')}
          />
          <span className="campo-ayuda" id="crear-companias-ayuda">
            Identificadores separados por coma. Al menos uno.
          </span>
          {errors.companiaIds && (
            <span className="error-campo" role="alert">
              {errors.companiaIds.message}
            </span>
          )}
        </div>

        <div className="dialogo-acciones">
          <button type="button" onClick={alCerrar}>
            Cancelar
          </button>
          <button type="submit" className="primario" disabled={crear.isPending}>
            {crear.isPending ? 'Creando…' : 'Crear usuario'}
          </button>
        </div>
      </form>
    </Dialogo>
  )
}

function FormularioEditarUsuario({
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

  const error = actualizar.error instanceof ApiError ? actualizar.error : undefined

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
            {error.message}
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
