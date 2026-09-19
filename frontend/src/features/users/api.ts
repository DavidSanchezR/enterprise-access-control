import { apiClient } from '../../lib/apiClient'

/** contracts/users.yaml — EstadoUsuario. Literales de texto, iguales a los persistidos. */
export const ESTADOS_USUARIO = ['ACTIVO', 'INACTIVO', 'BLOQUEADO'] as const
export type EstadoUsuario = (typeof ESTADOS_USUARIO)[number]

/** contracts/users.yaml — Usuario. */
export interface Usuario {
  id: string
  correo: string
  estado: EstadoUsuario
  requiereCambioPassword: boolean
  alcanceCompanias: string[]
}

/** contracts/users.yaml — PaginaUsuarios. */
export interface PaginaUsuarios {
  items: Usuario[]
  total: number
  pagina: number
  tamañoPagina: number
}

export interface FiltroUsuarios {
  estado?: EstadoUsuario
  pagina?: number
  tamañoPagina?: number
}

export async function listarUsuarios(filtro: FiltroUsuarios): Promise<PaginaUsuarios> {
  const { data } = await apiClient.get<PaginaUsuarios>('/api/usuarios', {
    params: {
      estado: filtro.estado,
      pagina: filtro.pagina,
      tamañoPagina: filtro.tamañoPagina,
    },
  })
  return data
}

export async function crearUsuario(entrada: {
  correo: string
  passwordInicial: string
  companiaIds: string[]
}): Promise<Usuario> {
  const { data } = await apiClient.post<Usuario>('/api/usuarios', entrada)
  return data
}

export async function actualizarUsuario(
  id: string,
  entrada: { correo: string; estado: EstadoUsuario },
): Promise<Usuario> {
  const { data } = await apiClient.put<Usuario>(`/api/usuarios/${id}`, entrada)
  return data
}

export async function desbloquearUsuario(id: string): Promise<void> {
  await apiClient.post(`/api/usuarios/${id}/desbloquear`)
}

export async function obtenerAlcance(id: string): Promise<string[]> {
  const { data } = await apiClient.get<string[]>(`/api/usuarios/${id}/alcance-companias`)
  return data
}

export async function reemplazarAlcance(id: string, companiaIds: string[]): Promise<void> {
  await apiClient.put(`/api/usuarios/${id}/alcance-companias`, { companiaIds })
}
