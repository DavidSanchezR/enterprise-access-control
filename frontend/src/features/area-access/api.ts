import { apiClient } from '../../lib/apiClient'
import type { Estado } from '../companies/api'

/** contracts/area-access.yaml — AreaAcceso. */
export interface AreaAcceso {
  id: string
  nombre: string
  areaSuperiorId: string | null
  /**
   * Dato propio del nodo, no resuelto por el servidor (RF-046).
   *
   * Es la diferencia con `UnidadOrganizativa`, cuya pertenencia se deduce recorriendo hasta la raíz
   * (RF-044). Aquí la Principal viaja en cada área, y por eso no es editable: cambiarla en un nodo
   * dejaría el árbol declarando dos dueños distintos.
   */
  companiaPrincipalId: string
  estado: Estado
}

/** contracts/area-access.yaml — NodoArbol. */
export interface NodoArbolArea {
  id: string
  nombre: string
  estado: Estado
  hijos: NodoArbolArea[]
}

export async function listarAreas(
  companiaPrincipalId: string,
  estado?: Estado,
): Promise<AreaAcceso[]> {
  const { data } = await apiClient.get<AreaAcceso[]>('/api/areas-acceso', {
    params: { companiaPrincipalId, estado },
  })
  return data
}

export async function obtenerArbolAreas(companiaPrincipalId: string): Promise<NodoArbolArea[]> {
  const { data } = await apiClient.get<NodoArbolArea[]>('/api/areas-acceso/arbol', {
    params: { companiaPrincipalId },
  })
  return data
}

export async function crearArea(entrada: {
  nombre: string
  estado: Estado
  areaSuperiorId?: string | null
  companiaPrincipalId?: string | null
}): Promise<AreaAcceso> {
  const { data } = await apiClient.post<AreaAcceso>('/api/areas-acceso', entrada)
  return data
}

export async function actualizarArea(
  id: string,
  entrada: { nombre: string; estado: Estado },
): Promise<AreaAcceso> {
  const { data } = await apiClient.put<AreaAcceso>(`/api/areas-acceso/${id}`, entrada)
  return data
}

export async function moverArea(id: string, nuevoPadreId: string | null): Promise<void> {
  await apiClient.post(`/api/areas-acceso/${id}/mover`, { nuevoPadreId })
}
