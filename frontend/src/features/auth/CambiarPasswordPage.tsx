import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import type { ReactElement } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../lib/apiClient'
import { cambiarPassword } from './api'
import { esquemaCambioPassword, type FormularioCambioPassword } from './esquemas'
import { useSesion } from './useSesion'
import './auth.css'

/**
 * Cambio de contraseña (RF-003). Es la pantalla obligatoria cuando el login devuelve
 * `requiereCambioPassword`, ya sea por expiración periódica o por alta hecha por un administrador.
 */
export function CambiarPasswordPage(): ReactElement {
  const { requiereCambioPassword, marcarPasswordCambiada } = useSesion()
  const navegar = useNavigate()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormularioCambioPassword>({
    resolver: zodResolver(esquemaCambioPassword),
    defaultValues: { passwordActual: '', passwordNueva: '', confirmacion: '' },
  })

  const mutacion = useMutation({
    mutationFn: (valores: FormularioCambioPassword) =>
      cambiarPassword(valores.passwordActual, valores.passwordNueva),
    onSuccess: () => {
      marcarPasswordCambiada()
      navegar('/', { replace: true })
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
        <h1>Cambiar contraseña</h1>

        {requiereCambioPassword && (
          <p className="aviso" role="status">
            Debe cambiar su contraseña antes de continuar.
          </p>
        )}

        {error && (
          <p className="aviso error" role="alert">
            {/*
              El detalle del servidor se muestra tal cual: es el único sitio que conoce la política
              vigente y qué requisito concreto se incumplió (o si la contraseña ya fue usada).
            */}
            {error.status === 401 ? 'La contraseña actual no es correcta.' : error.message}
          </p>
        )}

        <div className="campo">
          <label htmlFor="passwordActual">Contraseña actual</label>
          <input
            id="passwordActual"
            type="password"
            autoComplete="current-password"
            autoFocus
            aria-invalid={errors.passwordActual ? 'true' : undefined}
            aria-describedby={errors.passwordActual ? 'actual-error' : undefined}
            {...register('passwordActual')}
          />
          {errors.passwordActual && (
            <span className="error-campo" id="actual-error" role="alert">
              {errors.passwordActual.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="passwordNueva">Contraseña nueva</label>
          <input
            id="passwordNueva"
            type="password"
            autoComplete="new-password"
            aria-invalid={errors.passwordNueva ? 'true' : undefined}
            aria-describedby={errors.passwordNueva ? 'nueva-error' : undefined}
            {...register('passwordNueva')}
          />
          {errors.passwordNueva && (
            <span className="error-campo" id="nueva-error" role="alert">
              {errors.passwordNueva.message}
            </span>
          )}
        </div>

        <div className="campo">
          <label htmlFor="confirmacion">Repita la contraseña nueva</label>
          <input
            id="confirmacion"
            type="password"
            autoComplete="new-password"
            aria-invalid={errors.confirmacion ? 'true' : undefined}
            aria-describedby={errors.confirmacion ? 'confirmacion-error' : undefined}
            {...register('confirmacion')}
          />
          {errors.confirmacion && (
            <span className="error-campo" id="confirmacion-error" role="alert">
              {errors.confirmacion.message}
            </span>
          )}
        </div>

        <button type="submit" className="primario auth-boton" disabled={mutacion.isPending}>
          {mutacion.isPending ? 'Guardando…' : 'Cambiar contraseña'}
        </button>
      </form>
    </main>
  )
}
