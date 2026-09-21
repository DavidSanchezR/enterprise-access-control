import { apiClient, type RolAdministrativo } from '../../lib/apiClient'

/** contracts/users.yaml — EstadoUsuario. Literales de texto, iguales a los persistidos. */
export const ESTADOS_USUARIO = ['ACTIVO', 'INACTIVO', 'BLOQUEADO'] as const
export type EstadoUsuario = (typeof ESTADOS_USUARIO)[number]

/** contracts/users.yaml — AsignacionRolAdministrativo. */
export interface AsignacionRol {
  id: string
  usuarioId: string
  rol: RolAdministrativo
  /** NULL si y solo si el rol es GLOBAL_ADMINISTRATOR (RF-074). */
  companiaId: string | null
  fechaHoraInicio: string
  fechaHoraFin: string
  /** Derivado en el servidor: inicio <= ahora < fin. */
  vigente: boolean
}

/** contracts/users.yaml — Usuario. `asignacionesRol` trae solo las vigentes. */
export interface Usuario {
  id: string
  correo: string
  estado: EstadoUsuario
  requiereCambioPassword: boolean
  asignacionesRol: AsignacionRol[]
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
  /**
   * Búsqueda por correo. La resuelve el servidor sobre todo el conjunto dentro del alcance y antes de
   * paginar (RF-077, UX-22): no se filtra en el cliente, porque eso solo alcanzaría a la página ya
   * recibida.
   */
  texto?: string
  pagina?: number
  tamañoPagina?: number
}

/** Datos de una asignación nueva, compartidos por el alta de usuario y la asignación posterior. */
export interface DatosAsignacion {
  rol: RolAdministrativo
  companiaId: string | null
  fechaHoraInicio: string
  fechaHoraFin: string
}

export async function listarUsuarios(filtro: FiltroUsuarios): Promise<PaginaUsuarios> {
  const texto = filtro.texto?.trim()

  const { data } = await apiClient.get<PaginaUsuarios>('/api/usuarios', {
    params: {
      estado: filtro.estado,
      // Se omite en vez de enviarse vacío: un `texto=` sin valor no es un filtro.
      texto: texto === '' ? undefined : texto,
      pagina: filtro.pagina,
      tamañoPagina: filtro.tamañoPagina,
    },
  })
  return data
}

export async function crearUsuario(
  entrada: { correo: string; passwordInicial: string } & DatosAsignacion,
): Promise<Usuario> {
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

/** Asignaciones vigentes e históricas del usuario (contracts/users.yaml). */
export async function listarRoles(id: string): Promise<AsignacionRol[]> {
  const { data } = await apiClient.get<AsignacionRol[]>(`/api/usuarios/${id}/roles`)
  return data
}

/**
 * Agrega una asignación de rol; **no** reemplaza las existentes (RF-074, UX-19).
 */
export async function asignarRol(id: string, entrada: DatosAsignacion): Promise<AsignacionRol> {
  const { data } = await apiClient.post<AsignacionRol>(`/api/usuarios/${id}/roles`, entrada)
  return data
}

/** Cierra la vigencia de una asignación vigente (RF-075, UX-20). */
export async function finalizarRol(id: string, asignacionId: string): Promise<void> {
  await apiClient.post(`/api/usuarios/${id}/roles/${asignacionId}/finalizar`)
}

/**
 * Extiende la vigencia de una asignación vigente (RF-075, UX-20).
 *
 * Contraparte inversa de `finalizarRol`: nunca cambia rol ni compañía, y el servidor exige que
 * `fechaHoraFin` sea estrictamente posterior a la vigente.
 */
export async function renovarRol(
  id: string,
  asignacionId: string,
  fechaHoraFin: string,
): Promise<void> {
  await apiClient.post(`/api/usuarios/${id}/roles/${asignacionId}/renovar`, { fechaHoraFin })
}
