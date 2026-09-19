import type { ReactElement } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useSesion } from '../features/auth/useSesion'

const RUTA_CAMBIO_PASSWORD = '/cambiar-password'

/**
 * Guard de sesión para las rutas de administración.
 *
 * Es exclusivamente una ayuda de experiencia de usuario: evita mostrar pantallas vacías a quien no
 * ha iniciado sesión y lleva al cambio de contraseña a quien lo tiene pendiente. **No es una
 * frontera de seguridad** — toda autorización real se evalúa en el servidor (Constitución,
 * Principio I), que responde 401/403/404 aunque el cliente decida navegar.
 *
 * Lee la sesión con `useSesion` y no con `leerSesion()` directamente para volver a evaluarse cuando
 * la sesión cambia —por ejemplo, cuando un 401 la limpia desde el interceptor— y no sólo cuando hay
 * una navegación.
 */
export function RutaProtegida({ children }: { children: ReactElement }): ReactElement {
  const { autenticado, requiereCambioPassword } = useSesion()
  const location = useLocation()

  if (!autenticado) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }

  // Contraseña expirada o fijada por un administrador: no puede operar hasta cambiarla
  // (Historia 1, criterio 3). El propio destino se excluye para no producir un bucle.
  if (requiereCambioPassword && location.pathname !== RUTA_CAMBIO_PASSWORD) {
    return <Navigate to={RUTA_CAMBIO_PASSWORD} replace />
  }

  return children
}
