import { apiClient } from '../../lib/apiClient'
import type { Estado } from '../companies/api'

/** contracts/permissions.yaml — AlcancePermiso (RF-020). */
export const ALCANCES = ['PERSONA', 'UNIDAD_ORGANIZATIVA', 'COMPANIA'] as const
export type AlcancePermiso = (typeof ALCANCES)[number]

export const ETIQUETA_ALCANCE: Record<AlcancePermiso, string> = {
  PERSONA: 'Persona',
  UNIDAD_ORGANIZATIVA: 'Unidad organizativa',
  COMPANIA: 'Compañía',
}

/** contracts/permissions.yaml — DiaSemana, en el orden en que se muestra la semana. */
export const DIAS_SEMANA = [
  'LUNES',
  'MARTES',
  'MIERCOLES',
  'JUEVES',
  'VIERNES',
  'SABADO',
  'DOMINGO',
] as const
export type DiaSemana = (typeof DIAS_SEMANA)[number]

export const ETIQUETA_DIA: Record<DiaSemana, string> = {
  LUNES: 'Lunes',
  MARTES: 'Martes',
  MIERCOLES: 'Miércoles',
  JUEVES: 'Jueves',
  VIERNES: 'Viernes',
  SABADO: 'Sábado',
  DOMINGO: 'Domingo',
}

/** contracts/permissions.yaml — BloqueHorario. Horas locales America/Lima, formato HH:mm. */
export interface BloqueHorario {
  id?: string
  diaSemana: DiaSemana
  horaInicio: string
  horaFin: string
}

/** contracts/permissions.yaml — PermisoAcceso. */
export interface PermisoAcceso {
  id: string
  areaAccesoId: string
  alcance: AlcancePermiso
  personaId: string | null
  unidadOrganizativaId: string | null
  companiaId: string | null
  fechaHoraInicioVigencia: string
  /** Obligatoria para los tres alcances (RF-021, RF-071): no existe permiso de vigencia indefinida. */
  fechaHoraFinVigencia: string
  estado: Estado
  bloquesHorarios: BloqueHorario[]
}

/** contracts/permissions.yaml — PermisoAccesoRequest. */
export interface PermisoAccesoRequest {
  areaAccesoId: string
  alcance: AlcancePermiso
  personaId: string | null
  unidadOrganizativaId: string | null
  companiaId: string | null
  fechaHoraInicioVigencia: string
  fechaHoraFinVigencia: string
  estado: Estado
  bloquesHorarios: Omit<BloqueHorario, 'id'>[]
}

/** contracts/permissions.yaml — PaginaPermisos. */
export interface PaginaPermisos {
  items: PermisoAcceso[]
  total: number
  pagina: number
  tamañoPagina: number
}

export interface FiltroPermisos {
  areaAccesoId?: string
  personaId?: string
  unidadOrganizativaId?: string
  companiaId?: string
  estado?: Estado
  pagina?: number
  tamañoPagina?: number
}

export async function listarPermisos(filtro: FiltroPermisos): Promise<PaginaPermisos> {
  const { data } = await apiClient.get<PaginaPermisos>('/api/permisos', {
    params: {
      areaAccesoId: filtro.areaAccesoId || undefined,
      personaId: filtro.personaId || undefined,
      unidadOrganizativaId: filtro.unidadOrganizativaId || undefined,
      companiaId: filtro.companiaId || undefined,
      estado: filtro.estado,
      pagina: filtro.pagina,
      tamañoPagina: filtro.tamañoPagina,
    },
  })
  return data
}

export async function crearPermiso(entrada: PermisoAccesoRequest): Promise<PermisoAcceso> {
  const { data } = await apiClient.post<PermisoAcceso>('/api/permisos', entrada)
  return data
}

export async function actualizarPermiso(
  id: string,
  entrada: PermisoAccesoRequest,
): Promise<PermisoAcceso> {
  const { data } = await apiClient.put<PermisoAcceso>(`/api/permisos/${id}`, entrada)
  return data
}

/** Sujeto del permiso según su alcance: exactamente uno de los tres identificadores. */
export function sujetoDe(
  permiso: Pick<PermisoAcceso, 'alcance' | 'personaId' | 'unidadOrganizativaId' | 'companiaId'>,
): string | null {
  switch (permiso.alcance) {
    case 'PERSONA':
      return permiso.personaId
    case 'UNIDAD_ORGANIZATIVA':
      return permiso.unidadOrganizativaId
    case 'COMPANIA':
      return permiso.companiaId
  }
}
