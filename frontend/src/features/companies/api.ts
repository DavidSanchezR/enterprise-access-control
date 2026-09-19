import { apiClient } from '../../lib/apiClient'

/** contracts/companies.yaml — TipoCompania (RF-042). */
export const TIPOS_COMPANIA = ['PRINCIPAL_MANDANTE', 'CONTRATISTA'] as const
export type TipoCompania = (typeof TIPOS_COMPANIA)[number]

export const ESTADOS = ['ACTIVO', 'INACTIVO'] as const
export type Estado = (typeof ESTADOS)[number]

/** Etiquetas legibles; el valor persistido sigue siendo el literal del contrato. */
export const ETIQUETA_TIPO_COMPANIA: Record<TipoCompania, string> = {
  PRINCIPAL_MANDANTE: 'Principal mandante',
  CONTRATISTA: 'Contratista',
}

/** contracts/companies.yaml — Compania. */
export interface Compania {
  id: string
  nombre: string
  tipoDocumentoId: string
  numeroDocumento: string
  tipoCompania: TipoCompania
  estado: Estado
}

/** contracts/companies.yaml — PaginaCompanias. */
export interface PaginaCompanias {
  items: Compania[]
  total: number
  pagina: number
  tamañoPagina: number
}

export interface FiltroCompanias {
  estado?: Estado
  tipoCompania?: TipoCompania
  texto?: string
  pagina?: number
  tamañoPagina?: number
}

export interface CompaniaRequest {
  nombre: string
  tipoDocumentoId: string
  numeroDocumento: string
  tipoCompania: TipoCompania
  estado: Estado
}

/** contracts/companies.yaml — RelacionContratistaPrincipal. */
export interface RelacionContratistaPrincipal {
  id: string
  companiaContratistaId: string
  companiaPrincipalId: string
  fechaHoraInicio: string
  /** null = vigencia abierta: RF-071 no aplica a esta entidad, que vincula dos compañías. */
  fechaHoraFin: string | null
}

export async function listarCompanias(filtro: FiltroCompanias): Promise<PaginaCompanias> {
  const { data } = await apiClient.get<PaginaCompanias>('/api/companias', {
    params: {
      estado: filtro.estado,
      tipoCompania: filtro.tipoCompania,
      texto: filtro.texto || undefined,
      pagina: filtro.pagina,
      tamañoPagina: filtro.tamañoPagina,
    },
  })
  return data
}

export async function crearCompania(entrada: CompaniaRequest): Promise<Compania> {
  const { data } = await apiClient.post<Compania>('/api/companias', entrada)
  return data
}

export async function actualizarCompania(id: string, entrada: CompaniaRequest): Promise<Compania> {
  const { data } = await apiClient.put<Compania>(`/api/companias/${id}`, entrada)
  return data
}

export async function listarRelaciones(
  contratistaId: string,
): Promise<RelacionContratistaPrincipal[]> {
  const { data } = await apiClient.get<RelacionContratistaPrincipal[]>(
    `/api/companias/${contratistaId}/relaciones-principales`,
  )
  return data
}

export async function crearRelacion(
  contratistaId: string,
  entrada: { companiaPrincipalId: string; fechaHoraInicio: string },
): Promise<RelacionContratistaPrincipal> {
  const { data } = await apiClient.post<RelacionContratistaPrincipal>(
    `/api/companias/${contratistaId}/relaciones-principales`,
    entrada,
  )
  return data
}

export async function finalizarRelacion(contratistaId: string, relacionId: string): Promise<void> {
  await apiClient.post(
    `/api/companias/${contratistaId}/relaciones-principales/${relacionId}/finalizar`,
  )
}
