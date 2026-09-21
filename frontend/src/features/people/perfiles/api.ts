import { apiClient } from '../../../lib/apiClient'
import type { Estado } from '../../companies/api'

/**
 * contracts/people.yaml — AsignacionTipoPersona (RF-011).
 *
 * Es el "perfil" de la persona: qué tipo de persona es durante un intervalo. Hasta esta sesión no
 * existía ninguna pieza de frontend para esta entidad, pese a que el endpoint ya funcionaba (D7).
 */
export interface AsignacionTipoPersona {
  id: string
  personaId: string
  tipoPersonaId: string
  fechaHoraInicio: string
  fechaHoraFin: string
  estado: Estado
}

export interface AsignacionTipoPersonaRequest {
  tipoPersonaId: string
  fechaHoraInicio: string
  fechaHoraFin: string
}

export async function listarPerfiles(personaId: string): Promise<AsignacionTipoPersona[]> {
  const { data } = await apiClient.get<AsignacionTipoPersona[]>(
    `/api/personas/${personaId}/perfiles`,
  )
  return data
}

export async function asignarPerfil(
  personaId: string,
  entrada: AsignacionTipoPersonaRequest,
): Promise<AsignacionTipoPersona> {
  const { data } = await apiClient.post<AsignacionTipoPersona>(
    `/api/personas/${personaId}/perfiles`,
    entrada,
  )
  return data
}
