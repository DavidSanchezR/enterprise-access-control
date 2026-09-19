import { apiClient } from '../../lib/apiClient'

/** contracts/credentials.yaml — EstadoCredencial. ASIGNADO es el único estado no terminal. */
export type EstadoCredencial = 'ASIGNADO' | 'DEVUELTO' | 'ELIMINADO' | 'REVOCADA'

export const ETIQUETA_ESTADO_CREDENCIAL: Record<EstadoCredencial, string> = {
  ASIGNADO: 'Asignada',
  DEVUELTO: 'Devuelta',
  ELIMINADO: 'Dada de baja',
  REVOCADA: 'Revocada',
}

/** contracts/credentials.yaml — AsignacionCredencial. */
export interface AsignacionCredencial {
  id: string
  personaId: string
  companiaPrincipalId: string
  /** Tipo o diseño visual de la credencial; nunca una tecnología física (RF-058). */
  tipoCredencialId: string
  fechaHoraInicio: string
  /** Obligatoria desde la creación (RF-071): no existe credencial de vigencia indefinida. */
  fechaHoraFin: string
  estado: EstadoCredencial
  /** Pertenencia cuyo cese la revocó en cascada; solo si estado = REVOCADA (RF-061). */
  revocadoPorPertenenciaId: string | null
}

/** contracts/credentials.yaml — AsignacionCredencialRequest. La persona viaja en la ruta. */
export interface AsignacionCredencialRequest {
  companiaPrincipalId: string
  tipoCredencialId: string
  fechaHoraInicio: string
  fechaHoraFin: string
}

export async function listarCredenciales(
  personaId: string,
  companiaPrincipalId?: string,
): Promise<AsignacionCredencial[]> {
  const { data } = await apiClient.get<AsignacionCredencial[]>(
    `/api/personas/${personaId}/credenciales`,
    { params: { companiaPrincipalId: companiaPrincipalId || undefined } },
  )
  return data
}

export async function asignarCredencial(
  personaId: string,
  entrada: AsignacionCredencialRequest,
): Promise<AsignacionCredencial> {
  const { data } = await apiClient.post<AsignacionCredencial>(
    `/api/personas/${personaId}/credenciales`,
    entrada,
  )
  return data
}

export async function devolverCredencial(personaId: string, credencialId: string): Promise<void> {
  await apiClient.post(`/api/personas/${personaId}/credenciales/${credencialId}/devolver`)
}

/** Baja lógica: la credencial pasa a ELIMINADO y el registro se conserva. */
export async function eliminarCredencial(personaId: string, credencialId: string): Promise<void> {
  await apiClient.delete(`/api/personas/${personaId}/credenciales/${credencialId}`)
}

/**
 * Vigencia efectiva (RF-070): estado ASIGNADO y fecha dentro de la ventana, ambos extremos incluidos.
 *
 * Una credencial ASIGNADO cuya fecha de fin ya pasó está expirada aunque su estado no cambie.
 */
export function estaVigente(credencial: AsignacionCredencial, ahora: number = Date.now()): boolean {
  return (
    credencial.estado === 'ASIGNADO' &&
    new Date(credencial.fechaHoraInicio).getTime() <= ahora &&
    ahora <= new Date(credencial.fechaHoraFin).getTime()
  )
}
