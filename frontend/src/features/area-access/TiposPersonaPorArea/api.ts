import { apiClient } from '../../../lib/apiClient'

/** contracts/area-access.yaml — GET /api/areas-acceso/{id}/tipos-persona. */
export async function listarTiposPersonaDeArea(areaId: string): Promise<string[]> {
  const { data } = await apiClient.get<string[]>(`/api/areas-acceso/${areaId}/tipos-persona`)
  return data
}

/**
 * contracts/area-access.yaml — PUT /api/areas-acceso/{id}/tipos-persona (RF-019).
 *
 * Reemplaza el conjunto completo: lo que se envía es el estado final, no un incremento.
 */
export async function reemplazarTiposPersonaDeArea(
  areaId: string,
  tipoPersonaIds: string[],
): Promise<void> {
  await apiClient.put(`/api/areas-acceso/${areaId}/tipos-persona`, { tipoPersonaIds })
}
