import { apiClient } from '../../../lib/apiClient'
import type { Estado } from '../../companies/api'

/** contracts/people.yaml — EstadoPertenencia. */
export type EstadoPertenencia = 'ACTIVA' | 'FINALIZADA'

/** contracts/people.yaml — MotivoFinPertenencia. */
export type MotivoFinPertenencia = 'CESE_PERTENENCIA' | 'REEMPLAZO_ASIGNACION'

/** contracts/people.yaml — MotivoFinRevocacion. */
export type MotivoFinRevocacion =
  'REVOCACION_CESE_PERTENENCIA' | 'REEMPLAZO_ASIGNACION' | 'CIERRE_MANUAL'

/** Etiquetas legibles; el valor persistido sigue siendo el literal del contrato. */
export const ETIQUETA_MOTIVO_PERTENENCIA: Record<MotivoFinPertenencia, string> = {
  CESE_PERTENENCIA: 'Cese de pertenencia',
  REEMPLAZO_ASIGNACION: 'Reemplazada por otra',
}

export const ETIQUETA_MOTIVO_REVOCACION: Record<MotivoFinRevocacion, string> = {
  REVOCACION_CESE_PERTENENCIA: 'Revocado al cesar la pertenencia',
  REEMPLAZO_ASIGNACION: 'Reemplazado por otro',
  CIERRE_MANUAL: 'Cierre manual',
}

/** contracts/people.yaml — AsignacionCompania. */
export interface AsignacionCompania {
  id: string
  personaId: string
  companiaId: string
  fechaHoraInicio: string
  /** Obligatoria desde la creación (RF-071): no existe pertenencia de vigencia indefinida. */
  fechaHoraFin: string
  estado: EstadoPertenencia
  motivoFin: MotivoFinPertenencia | null
}

/** contracts/people.yaml — ContextoOperativo. */
export interface ContextoOperativo {
  id: string
  personaId: string
  companiaPrincipalId: string
  fechaHoraInicio: string
  fechaHoraFin: string
  estado: Estado
  motivoFin: MotivoFinRevocacion | null
  /** Poblado solo si el cierre vino de la cascada (RF-061). */
  revocadoPorPertenenciaId: string | null
}

/** contracts/people.yaml — AsignacionUnidadOrganizativa. */
export interface AsignacionUnidadOrganizativa {
  id: string
  personaId: string
  contextoOperativoId: string
  unidadOrganizativaId: string
  fechaHoraInicio: string
  fechaHoraFin: string
  estado: Estado
  motivoFin: MotivoFinRevocacion | null
  revocadoPorPertenenciaId: string | null
}

export async function listarHistorialCompanias(personaId: string): Promise<AsignacionCompania[]> {
  const { data } = await apiClient.get<AsignacionCompania[]>(
    `/api/personas/${personaId}/historial-companias`,
  )
  return data
}

export async function crearPertenencia(
  personaId: string,
  entrada: { companiaId: string; fechaHoraInicio: string; fechaHoraFin: string },
): Promise<AsignacionCompania> {
  const { data } = await apiClient.post<AsignacionCompania>(
    `/api/personas/${personaId}/historial-companias`,
    entrada,
  )
  return data
}

export async function finalizarPertenencia(
  personaId: string,
  asignacionId: string,
  fechaHoraFin: string,
): Promise<void> {
  await apiClient.post(`/api/personas/${personaId}/historial-companias/${asignacionId}/finalizar`, {
    fechaHoraFin,
  })
}

export async function renovarPertenencia(
  personaId: string,
  asignacionId: string,
  fechaHoraFin: string,
): Promise<void> {
  await apiClient.post(`/api/personas/${personaId}/historial-companias/${asignacionId}/renovar`, {
    fechaHoraFin,
  })
}

export async function listarContextos(personaId: string): Promise<ContextoOperativo[]> {
  const { data } = await apiClient.get<ContextoOperativo[]>(
    `/api/personas/${personaId}/contextos-operativos`,
  )
  return data
}

export async function abrirContexto(
  personaId: string,
  entrada: { companiaPrincipalId: string; fechaHoraInicio: string; fechaHoraFin: string },
): Promise<ContextoOperativo> {
  const { data } = await apiClient.post<ContextoOperativo>(
    `/api/personas/${personaId}/contextos-operativos`,
    entrada,
  )
  return data
}

export async function listarUnidadesDelContexto(
  personaId: string,
  contextoId: string,
): Promise<AsignacionUnidadOrganizativa[]> {
  const { data } = await apiClient.get<AsignacionUnidadOrganizativa[]>(
    `/api/personas/${personaId}/contextos-operativos/${contextoId}/unidad-organizativa`,
  )
  return data
}

export async function asignarUnidad(
  personaId: string,
  contextoId: string,
  entrada: { unidadOrganizativaId: string; fechaHoraInicio: string; fechaHoraFin: string },
): Promise<AsignacionUnidadOrganizativa> {
  const { data } = await apiClient.post<AsignacionUnidadOrganizativa>(
    `/api/personas/${personaId}/contextos-operativos/${contextoId}/unidad-organizativa`,
    entrada,
  )
  return data
}
