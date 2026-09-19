import { apiClient } from '../../lib/apiClient'
import type { Estado } from '../companies/api'

/** contracts/org-units.yaml — UnidadOrganizativa. */
export interface UnidadOrganizativa {
  id: string
  nombre: string
  unidadSuperiorId: string | null
  /** Resuelto por el servidor recorriendo hasta la raíz: la entidad no lo almacena (RF-044). */
  companiaPrincipalId: string
  estado: Estado
}

/** contracts/org-units.yaml — NodoArbol. */
export interface NodoArbolUnidad {
  id: string
  nombre: string
  estado: Estado
  hijos: NodoArbolUnidad[]
}

export async function listarUnidades(
  companiaPrincipalId: string,
  estado?: Estado,
): Promise<UnidadOrganizativa[]> {
  const { data } = await apiClient.get<UnidadOrganizativa[]>('/api/unidades-organizativas', {
    params: { companiaPrincipalId, estado },
  })
  return data
}

export async function obtenerArbol(companiaPrincipalId: string): Promise<NodoArbolUnidad[]> {
  const { data } = await apiClient.get<NodoArbolUnidad[]>('/api/unidades-organizativas/arbol', {
    params: { companiaPrincipalId },
  })
  return data
}

export async function crearUnidad(entrada: {
  nombre: string
  estado: Estado
  unidadSuperiorId?: string | null
  companiaPrincipalId?: string | null
}): Promise<UnidadOrganizativa> {
  const { data } = await apiClient.post<UnidadOrganizativa>('/api/unidades-organizativas', entrada)
  return data
}

export async function actualizarUnidad(
  id: string,
  entrada: { nombre: string; estado: Estado },
): Promise<UnidadOrganizativa> {
  const { data } = await apiClient.put<UnidadOrganizativa>(
    `/api/unidades-organizativas/${id}`,
    entrada,
  )
  return data
}

export async function moverUnidad(id: string, nuevoPadreId: string | null): Promise<void> {
  await apiClient.post(`/api/unidades-organizativas/${id}/mover`, { nuevoPadreId })
}
