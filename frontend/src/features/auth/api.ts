import { apiClient } from '../../lib/apiClient'

/** contracts/auth.yaml — LoginResponse. */
export interface LoginResponse {
  accessToken: string
  expiraEn: string
  requiereCambioPassword: boolean
  alcanceCompanias: string[]
}

/** contracts/auth.yaml — SesionActual. */
export interface SesionActual {
  usuarioId: string
  correo: string
  alcanceCompanias: string[]
}

export async function login(correo: string, password: string): Promise<LoginResponse> {
  const { data } = await apiClient.post<LoginResponse>('/api/auth/login', { correo, password })
  return data
}

export async function cambiarPassword(
  passwordActual: string,
  passwordNueva: string,
): Promise<void> {
  await apiClient.post('/api/auth/cambiar-password', { passwordActual, passwordNueva })
}

export async function obtenerSesion(): Promise<SesionActual> {
  const { data } = await apiClient.get<SesionActual>('/api/auth/sesion')
  return data
}
