import { apiClient } from '../../lib/apiClient'
import type { Estado } from '../companies/api'

/** contracts/masters.yaml — MasterItem. */
export interface MasterItem {
  id: string
  nombre: string
  estado: Estado
}

/** contracts/masters.yaml — MasterItemRequest. */
export interface MasterItemRequest {
  nombre: string
  estado: Estado
}

/**
 * Los cinco catálogos que expone `contracts/masters.yaml`.
 *
 * `limiteNombre` reproduce la longitud de columna que declara data-model.md para cada uno. El
 * contrato publica un máximo genérico de 200, pero las columnas son más estrechas (10 para tipo de
 * sangre): validar con el límite real evita ofrecer al usuario un texto que el servidor va a
 * rechazar.
 */
export const CATALOGOS = [
  { ruta: 'tipos-documento', titulo: 'Tipos de documento', limiteNombre: 100 },
  { ruta: 'tipos-sangre', titulo: 'Tipos de sangre', limiteNombre: 10 },
  { ruta: 'generos', titulo: 'Géneros', limiteNombre: 50 },
  { ruta: 'tipos-persona', titulo: 'Tipos de persona', limiteNombre: 100 },
  { ruta: 'tipos-credencial', titulo: 'Tipos de credencial', limiteNombre: 100 },
] as const

export type RutaCatalogo = (typeof CATALOGOS)[number]['ruta']

export type Catalogo = (typeof CATALOGOS)[number]

export function catalogoPorRuta(ruta: string): Catalogo | undefined {
  return CATALOGOS.find((c) => c.ruta === ruta)
}

export async function listarMaestro(ruta: RutaCatalogo, estado?: Estado): Promise<MasterItem[]> {
  const { data } = await apiClient.get<MasterItem[]>(`/api/maestros/${ruta}`, {
    params: { estado },
  })
  return data
}

export async function crearMaestro(
  ruta: RutaCatalogo,
  entrada: MasterItemRequest,
): Promise<MasterItem> {
  const { data } = await apiClient.post<MasterItem>(`/api/maestros/${ruta}`, entrada)
  return data
}

export async function actualizarMaestro(
  ruta: RutaCatalogo,
  id: string,
  entrada: MasterItemRequest,
): Promise<MasterItem> {
  const { data } = await apiClient.put<MasterItem>(`/api/maestros/${ruta}/${id}`, entrada)
  return data
}
