import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../../lib/apiClient'
import { login } from './api'
import { esquemaLogin, type FormularioLogin } from './esquemas'
import { useSesion } from './useSesion'
import './auth.css'

interface EstadoNavegacion {
  from?: string
}

/**
 * Inicio de sesión (Historia 1, RF-001 a RF-003).
 */
export function LoginPage(): ReactElement {
  const { iniciar } = useSesion()
  const navegar = useNavigate()
  const ubicacion = useLocation()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormularioLogin>({
    resolver: zodResolver(esquemaLogin),
    defaultValues: { correo: '', password: '' },
  })

  const mutacion = useMutation({
    mutationFn: (valores: FormularioLogin) => login(valores.correo, valores.password),
    onSuccess: (respuesta) => {
      iniciar({
        accessToken: respuesta.accessToken,
        expiraEn: respuesta.expiraEn,
        rol: respuesta.rol,
        companiaIds: respuesta.companiaIds,
        requiereCambioPassword: respuesta.requiereCambioPassword,
      })

      // Una contraseña expirada o fijada por un administrador no impide entrar, pero obliga a
      // cambiarla antes de operar (Historia 1, criterio 3).
      if (respuesta.requiereCambioPassword) {
        navegar('/cambiar-password', { replace: true })
        return
      }

      const destino = (ubicacion.state as EstadoNavegacion | null)?.from ?? '/'
      navegar(destino, { replace: true })
    },
  })

  const error = mutacion.error instanceof ApiError ? mutacion.error : undefined

  return (
    <main className="auth-pagina">
      <form
        className="auth-tarjeta tarjeta"
        onSubmit={(evento) => {
          void handleSubmit((valores) => mutacion.mutate(valores))(evento)
        }}
        noValidate
      >
        <h1>Control de Acceso Empresarial</h1>
        <p className="auth-subtitulo">Ingrese con su cuenta corporativa.</p>

        {error && (
          <p className="aviso error" role="alert">
            {error.status === 403 ? error.message : 'Correo o contraseña incorrectos.'}
          </p>
        )}

        <div className="campo">
          <label htmlFor="correo">Correo</label>
          <input
            id="correo"
            type="email"
            autoComplete="username"
            autoFocus
            aria-invalid={errors.correo ? 'true' : undefined}
            aria-describedby={errors.correo ? 'correo-error' : undefined}
            {...register('correo')}
          />
          {errors.correo && (
            <span className="error-campo" id="correo-error" role="alert">
              {errors.correo.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="password">Contraseña</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            aria-invalid={errors.password ? 'true' : undefined}
            aria-describedby={errors.password ? 'password-error' : undefined}
            {...register('password')}
          />
          {errors.password && (
            <span className="error-campo" id="password-error" role="alert">
              {errors.password.message}
            </span>
          )}
        </div>

        <button type="submit" className="primario auth-boton" disabled={mutacion.isPending}>
          {mutacion.isPending ? 'Ingresando…' : 'Ingresar'}
        </button>
      </form>
    </main>
  )
}
